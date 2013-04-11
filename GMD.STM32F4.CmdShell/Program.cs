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
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;

using GMD.STM32F4.CmdShell.Messaging;
using GMD.STM32F4.CmdShell.RNG;
using GMD.STM32F4.Hardware;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace GMD.STM32F4.CmdShell
{

    /// <summary>
    /// STM32F4 Command "Shell"
    /// 
    /// </summary>
    public class Program
    {
        private class IndicatorState
        {
            public int Count;
            public int Limit;

            public IndicatorState(int count, int limit)
            {
                Count = count;
                Limit = limit;
            }
        }

        private static MessagingIO _ioHandler;
        private static Hashtable _msgDispatch = new Hashtable();
        private static ArrayList _cmdHandlers = new ArrayList();
        private static ManualResetEvent _stopSignal = new ManualResetEvent(false);
        private static ManualResetEvent _rebootSignal = new ManualResetEvent(false);
        private static int _indicatorInterval = 250;

        public static void IndicatorThreadProc()
        {
            OutputPort testLED = new OutputPort(Pin.PC0, false);
            while (!_stopSignal.WaitOne(_indicatorInterval, false))
            {
                testLED.Write(!testLED.Read());
            }
        }

        public static void Main()
        {
            Debug.EnableGCMessages(true);

            Thread indicatorThread = new Thread(IndicatorThreadProc);
            indicatorThread.Start();

            RegisterHandlers();

            _ioHandler = new MessagingIO("COM2", 460800, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
            _ioHandler.MessageReceived += new MessageEventDelegate(ioHandler_MessageReceived);
            _ioHandler.AlertNotify += new AlertDelegate(_ioHandler_AlertNotify);
            _ioHandler.DataReceived += new DataReceivedDelegate(_ioHandler_DataReceived);

            try
            {
                _ioHandler.Start();
            }
            catch(Exception e)
            {
                Debug.Print("Exception opening COM port: " + e.Message + "- " + e.StackTrace);
            }

//#if DEBUG
            //_ioHandler.ResultSent += new ResultEventDelegate(_ioHandler_ResultSent);
            //DebugOutHelp();
            //DebugCallCommandsNoParams();
            //DebugCallCommandsWithParams();
//#endif

            while ( !_stopSignal.WaitOne(0, false) )
                Thread.Sleep(1000);

            _ioHandler.Stop();

            UnregisterHandlers();

            indicatorThread.Join();

            if (_rebootSignal.WaitOne(0,false))
                PowerState.RebootDevice(false);
        }

        #region Public Methods
        public static string GetAllHelp()
        {
            string[] helps = new string[_msgDispatch.Count];
            int i = 0;

            foreach (CmdHandlerDef dispatch in _msgDispatch.Values)
            {
                helps[i++] = dispatch.Cmd + " - " + dispatch.HelpString;
            }

            SortArrayOfString(helps);

            StringBuilder sb = new StringBuilder();
            foreach (string s in helps)
                sb.AppendLine(s);

            return sb.ToString();
        }

        public static string GetCommandHelp(string cmd)
        {
            if (cmd == null || cmd.Length == 0)
                return GetAllHelp();

            var vdef = _msgDispatch[cmd.ToLower()];

            if (vdef != null)
            {
                return ((CmdHandlerDef)vdef).HelpString;
            }

            return "";
        }

        public static void SendResponse(Result resp)
        {
            _indicatorInterval = 25;
            _ioHandler.SendAsynchronousResult(resp);
//            Thread.Sleep(1000);
            _indicatorInterval = 250;
        }

        public static bool SendUnsolicitedMsg(Message msg)
        {
            ResultCode result = _ioHandler.SendUnsolicitedMessage(msg);

            return result == ResultCode.Success;
        }

        public static void HandleParentCommand(string cmd)
        {
            switch (cmd.ToLower())
            {
                case "shutdown":
                    _stopSignal.Set();
                    break;
                case "reboot":
                    _rebootSignal.Set();
                    _stopSignal.Set();
                    break;
                default:
                    break;
            }
        }
        #endregion Public Methods

        #region Private Methods
        private static void _ioHandler_DataReceived(int byteCount)
        {
            //_indicatorInterval = 10;
            //Thread.Sleep(10 * byteCount);
            //_indicatorInterval = 250;
        }

        private static void _ioHandler_AlertNotify(string alert, Exception e)
        {
            _indicatorInterval = 0;
        }

        private static void ioHandler_MessageReceived(Message msg)
        {
            var handlerDef = _msgDispatch[msg.Command.ToLower()];

            if (handlerDef != null)
                ((CmdHandlerDef)handlerDef).CmdProc.Invoke(msg);
        }

        private static void SortArrayOfString(string[] items)
        {
            string temp;
            int n = items.Length;
            bool swapped = true;

            while(swapped)
            {
                swapped = false;
                
                for ( int i = 1; i <= n - 1; i++ )
                {
                    if ( ((string)items[i-1]).CompareTo(((string)items[i])) > 0 )
                    {
                        temp = items[i-1];
                        items[i-1] = items[i];
                        items[i] = temp;
                        swapped = true;
                    }
                }
            }
        }

        private static void _ioHandler_ResultSent(Result rslt)
        {
            Debug.Print(rslt.ToString());
        }

        /// <summary>
        /// This is where IMessageHandler objects are registered
        /// </summary>
        private static void RegisterHandlers()
        {
            //Note: Currently only supporting one handler for a given command.
            //If more than one handler uses the same command, only the last 
            //registered will actually handle the command.
            //Therefore, register "higher" handlers, such as SysCmd, last.
            _cmdHandlers.Add(new IOCmdHandler());
            _cmdHandlers.Add(new SysCmdHandler());
            _cmdHandlers.Add(new RNGCmdHandler());

            foreach (IMessageHandler mh in _cmdHandlers)
            {
                mh.RegisterParentHandler(HandleParentCommand);
                mh.RegisterParentHelpHandler(GetCommandHelp);
                mh.RegisterParentResponseHandler(SendResponse);
                mh.RegisterParentUnsolicitedMsgHandler(SendUnsolicitedMsg);

                foreach(CmdHandlerDef hd in mh.GetCmdHandlers())
                {
                    _msgDispatch.Add(hd.Cmd.ToLower(), hd);
                }

                mh.Start();
            }
        }

        private static void UnregisterHandlers()
        {
            foreach (IMessageHandler mh in _cmdHandlers)
            {
                mh.UnregisterParentHandler(HandleParentCommand);
                mh.UnregisterParentHelpHandler(GetCommandHelp);
                mh.UnregisterParentResponseHandler(SendResponse);
                mh.UnregisterParentUnsolicitedMsgHandler(SendUnsolicitedMsg);

                foreach (CmdHandlerDef hd in mh.GetCmdHandlers())
                {
                    _msgDispatch.Remove(hd.Cmd.ToLower());
                }

                mh.Stop();
            }
        }

        private static CmdHandlerProc GetHandler(string cmd)
        {
            var handlerDef = _msgDispatch[cmd.ToLower()];

            if (handlerDef != null)
            {
                return ((CmdHandlerDef)handlerDef).CmdProc;
            }

            return null;
        }

#if F4DISCOVERY
        private static void IndicateIncomingData(object state)
        {
            IndicatorState myState = (IndicatorState)state;

            LED_Green.Write(LED_Green.Read());

            if (++myState.Count == myState.Limit)
                incomingTimer.Change(0, 0);

            LED_Green.Write(false);
        }

        private static void IndicateOutgoingData(object state)
        {
            IndicatorState myState = (IndicatorState)state;

            LED_Blue.Write(LED_Blue.Read());

            if (++myState.Count == myState.Limit)
                outgoingTimer.Change(0, 0);

            LED_Blue.Write(false);
        }

        private static void IndicateError(bool error)
        {
            LED_Red.Write(error);
        }

        private static void IndicateWarning(bool warning)
        {
            LED_Orange.Write(warning);
        }
#endif

#if DEBUG
        private static void DebugOutHelp()
        {
            Debug.Print("\n BEGIN Command Help Debug Dump\n");

            IEnumerator myEnum = _msgDispatch.GetEnumerator();
            while (myEnum.MoveNext())
            {
                CmdHandlerDef cmdHandler = (CmdHandlerDef)((DictionaryEntry)myEnum.Current).Value;
                Debug.Print(cmdHandler.Cmd.ToLower() + ": " + cmdHandler.HelpString);
            }

            Debug.Print("\n END Command Help Debug Dump\n");
        }

        private static void DebugCallCommandsNoParams()
        {
            ulong i = 0;
            short j = 0;

            IEnumerator myEnum = _msgDispatch.GetEnumerator();
            while (myEnum.MoveNext())
            {
                CmdHandlerDef cmdHandler = (CmdHandlerDef)((DictionaryEntry)myEnum.Current).Value;

                var handlerDef = _msgDispatch[cmdHandler.Cmd.ToLower()];

                if (handlerDef != null && cmdHandler.Cmd.ToLower() != "shutdown" ) //Best to not shutdown in middle of unit testing
                {
                    switch (cmdHandler.Cmd.ToLower())
                    {
                        case "readinput":
                        case "set":
                        case "setoutput":
                        case "getpinmap":
                            continue;
                            break;
                        default:
                            break;
                    }

                    Debug.Print("BEGIN " + cmdHandler.Cmd.ToLower() + ": " + cmdHandler.HelpString);

                    Message msg = new Message(i++, j++, cmdHandler.Cmd.ToLower(), null);
                    ((CmdHandlerDef)handlerDef).CmdProc.Invoke(msg);

                    Debug.Print("END " + cmdHandler.Cmd.ToLower() + ": " + cmdHandler.HelpString + "\n\n");
                }
            }
        }

        private static void DebugCallCommandsWithParams()
        {
            ulong k = 1000;
            short l = 1000;

            IEnumerator myEnum = _msgDispatch.GetEnumerator();
            while (myEnum.MoveNext())
            {
                CmdHandlerDef cmdHandler = (CmdHandlerDef)((DictionaryEntry)myEnum.Current).Value;

                var handlerDef = _msgDispatch[cmdHandler.Cmd.ToLower()];

                if (handlerDef != null && cmdHandler.Cmd.ToLower() != "shutdown")
                {
                    string[] args = null;

                    switch (cmdHandler.Cmd.ToLower())
                    {
                        case "readinput":
                            args = new string[] {"7"};
                            break;
                        case "set":
                            args = new string[] {"0101010101010101" };
                            break;
                        case "setoutput":
                            args = new string[] {"7", "1"};
                            break;
                        case "getpinmap":
                            args = new string[] { "input" };
                            break;
                        default:
                            continue;
                            break;
                    }

                    Debug.Print("BEGIN " + cmdHandler.Cmd.ToLower() + ": " + cmdHandler.HelpString);

                    Message msg = new Message(k++, l++, cmdHandler.Cmd.ToLower(), args);
                    ((CmdHandlerDef)handlerDef).CmdProc.Invoke(msg);

                    Debug.Print("END " + cmdHandler.Cmd.ToLower() + "\n\n");
                }
            }
        }
#endif
        #endregion Private Methods
    }
}
