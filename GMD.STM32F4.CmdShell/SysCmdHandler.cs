/******************************************************************************/
// Copyright © 2012 GMD Communications
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy
// of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See
// the License for the specific language governing permissions and limitations
// under the License. 
//
// Author: Gregg DeMasters
// Date Created: 2012-09-16
//
/******************************************************************************/
using System;
using System.Reflection;
using System.Text;
using System.Threading;

using GMD.STM32F4.CmdShell.Messaging;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace GMD.STM32F4.CmdShell
{
    /// <summary>
    /// "System" command handler.  Handles commands such as version,
    /// help, time, etc.
    /// </summary>
    public class SysCmdHandler : IMessageHandler
    {
        #region Private Members
        private Result _lastResult = null;
        private ManualResetEvent _busySignal;
        private CmdHandlerHelpProc _parentHelp;
        private CmdHandlerResponseProc _parentResponse;
        private CmdHandlerProc _parentUnsolicitedMsgProc;
        private CmdHandlerParentHandlerProc _parentHandlerProc;
        #endregion Private Members

        public SysCmdHandler()
        {
            _busySignal = new ManualResetEvent(false);
        }

        #region IMessageHandler Methods
        public void Start()
        {
        }

        public void Stop()
        {
        }

        public bool ReadyForCommand
        {
            get { return !_busySignal.WaitOne(0, false); }
        }

        public Result LastResult
        {
            get { return _lastResult; }
        }

        public void RegisterParentHelpHandler(CmdHandlerHelpProc parentHelpProc)
        {
            _parentHelp = parentHelpProc;
        }

        public void RegisterParentResponseHandler(CmdHandlerResponseProc parentResponseProc)
        {
            _parentResponse = parentResponseProc;
        }

        public void RegisterParentUnsolicitedMsgHandler(CmdHandlerProc parentUnsolicitedMsgProc)
        {
            _parentUnsolicitedMsgProc = parentUnsolicitedMsgProc;
        }

        public void RegisterParentHandler(CmdHandlerParentHandlerProc parentHandlerProc)
        {
            _parentHandlerProc = parentHandlerProc;
        }

        public void UnregisterParentHelpHandler(CmdHandlerHelpProc parentHelpProc)
        {
            _parentHelp = null;
        }

        public void UnregisterParentResponseHandler(CmdHandlerResponseProc parentResponseProc)
        {
            _parentResponse = null;
        }

        public void UnregisterParentUnsolicitedMsgHandler(CmdHandlerProc parentUnsolicitedMsgProc)
        {
            _parentUnsolicitedMsgProc = null;
        }

        public void UnregisterParentHandler(CmdHandlerParentHandlerProc parentHandlerProc)
        {
        }
        #endregion


        public CmdHandlerDef[] GetCmdHandlers()
        {
            CmdHandlerDef[] retVal = new CmdHandlerDef[11];

            retVal[0] = new CmdHandlerDef("help", new CmdHandlerProc(CmdHandler_Help), "Display system help");
            retVal[1] = new CmdHandlerDef("h", new CmdHandlerProc(CmdHandler_Help), "Display system help");
            retVal[2] = new CmdHandlerDef("?", new CmdHandlerProc(CmdHandler_Help), "Display system help");
            retVal[3] = new CmdHandlerDef("ver", new CmdHandlerProc(CmdHandler_Version), "Display CommandShell version");
            retVal[4] = new CmdHandlerDef("version", new CmdHandlerProc(CmdHandler_Version), "Display CommandShell version");
            retVal[5] = new CmdHandlerDef("time", new CmdHandlerProc(CmdHandler_Time), "Get/Set CommandShell time (param format: 23:59:59)");
            retVal[6] = new CmdHandlerDef("date", new CmdHandlerProc(CmdHandler_Date), "Get/Set CommandShell date (param format: 12/31/2012)");
            retVal[7] = new CmdHandlerDef("info", new CmdHandlerProc(CmdHandler_Sysinfo), "Display CommandShell system info");
            retVal[8] = new CmdHandlerDef("shutdown", new CmdHandlerProc(CmdHandler_Shutdown), "Shutdown the system");
            retVal[9] = new CmdHandlerDef("ping", new CmdHandlerProc(CmdHandler_Ping), "Request acknowledgement from the system");
            retVal[10] = new CmdHandlerDef("reboot", new CmdHandlerProc(CmdHandler_Reboot), "Reboot the system [milliseconds]");

            return retVal;
        }

        /// <summary>
        /// These are message handlers registered with the parent to handle incoming commands.
        /// </summary>
        /// <returns></returns>
        #region Command Handlers
        public bool CmdHandler_Ping(Message msg)
        {
            _busySignal.Set();

            Result response = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.None, null);
            _parentResponse(response);
            _lastResult = response;

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Help(Message msg)
        {
            _busySignal.Set();

            string helpText;

            if (msg.Args != null && msg.Args.Length > 0) //Give help for command give as parameter
            {
                helpText = _parentHelp(msg.Args[0]);
            }
            else //Give system help
            {
                helpText = _parentHelp("");
            }

            Result response = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.String, Encoding.UTF8.GetBytes(helpText));
            _parentResponse(response);
            _lastResult = response;

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Version(Message msg)
        {
            _busySignal.Set();

            Result response = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.String, Encoding.UTF8.GetBytes(Assembly.GetExecutingAssembly().FullName));
            _parentResponse(response);
            _lastResult = response;

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Time(Message msg)
        {
            _busySignal.Set();

            if (msg.Args != null && msg.Args.Length == 1)
            {
                DateTime now = DateTime.Now;
                string[] ints = msg.Args[0].Split(':');

                try
                {
                    int hour = System.Convert.ToInt32(ints[0]);
                    int minute = System.Convert.ToInt32(ints[1]);
                    int second = System.Convert.ToInt32(ints[2]);

                    //Set system time
                    DateTime newTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, second);
                    Utility.SetLocalTime(newTime);
                }
                catch{}
            }

            Result response = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.String, Encoding.UTF8.GetBytes(DateTime.Now.ToString("HH:mm:ss.fff")));
            _parentResponse(response);
            _lastResult = response;

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Date(Message msg)
        {
            _busySignal.Set();

            if (msg.Args != null && msg.Args.Length == 1)
            {
                DateTime now = DateTime.Now;
                string[] ints = msg.Args[0].Split('/');

                try
                {
                    int month = System.Convert.ToInt32(ints[0]);
                    int day = System.Convert.ToInt32(ints[1]);
                    int year = System.Convert.ToInt32(ints[2]);

                    //Set system time
                    DateTime newTime = new DateTime(year, month, day, now.Hour, now.Minute, now.Second);
                    Utility.SetLocalTime(newTime);
                }
                catch { }
            }

            Result response = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.String, Encoding.UTF8.GetBytes(DateTime.Now.ToString("MM/dd/yyyy")));
            _parentResponse(response);
            _lastResult = response;

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Sysinfo(Message msg)
        {
            _busySignal.Set();

            Result response = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.String, Encoding.UTF8.GetBytes("GMD CommandShell for the STM32F4"));
            _parentResponse(response);
            _lastResult = response;

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Shutdown(Message msg)
        {
            _busySignal.Set();

            Result response = new Result(msg.Sequence, msg.CallBackID, ResultCode.OperationPending, ResultType.String, Encoding.UTF8.GetBytes("Shutting down...")); 
            _parentHandlerProc(msg.Command);
            _lastResult = null;

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Reboot(Message msg)
        {
            _busySignal.Set();

            Result response = new Result(msg.Sequence, msg.CallBackID, ResultCode.OperationPending, ResultType.String, Encoding.UTF8.GetBytes("Rebooting..."));

            if ( msg.Args != null )
            {
                int delay = System.Convert.ToInt32(msg.Args[0]);
                Thread.Sleep(delay);
            }

            _parentHandlerProc(msg.Command);
            _lastResult = null;
            _busySignal.Reset();
            return true;
        }
        #endregion
    }
}
