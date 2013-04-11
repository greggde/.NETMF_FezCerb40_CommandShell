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
using System.Text;
using System.Threading;

using GMD.STM32F4.CmdShell.Messaging;
using GMD.STM32F4.Hardware;
using Microsoft.SPOT.Hardware;

namespace GMD.STM32F4.CmdShell.RNG
{
    public class RNGCmdHandler : IMessageHandler
    {
        #region Private Members
        private Result _lastResult = null;
        private ManualResetEvent _busySignal;
        private CmdHandlerHelpProc _parentHelp;
        private CmdHandlerResponseProc _parentResponse;
        private CmdHandlerProc _parentUnsolicitedMsgProc;

        private GMD.STM32F4.Hardware.RNG _rng;
        private uint _firstDraw;
        #endregion Private Members

        #region Constants
        #endregion Constants

        public RNGCmdHandler()
        {
            _busySignal = new ManualResetEvent(false);
        }

        #region IMessageHandler Methods
        public void Start()
        {
            _rng = new GMD.STM32F4.Hardware.RNG();
            _rng.EnableRNG(true);
            _firstDraw = _rng.GetRandom();
        }

        public void Stop()
        {
            _rng.Shutdown();
            _rng.Dispose();
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
            CmdHandlerDef[] retVal = new CmdHandlerDef[1];

            retVal[0] = new CmdHandlerDef("getrnd", new CmdHandlerProc(CmdHandler_GetRandom), "Get a hardware generated random number (uint)");

            return retVal;
        }

        /// <summary>
        /// These are message handlers registered with the parent to handle incoming commands.
        /// </summary>
        /// <returns></returns>
        #region Command Handlers
        public bool CmdHandler_GetRandom(Message msg)
        {
            _busySignal.Set();

            try
            {
                if (_rng != null)
                {
                    uint rand = _rng.GetRandom();

                    Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.UInt, GMD.STM32F4.CmdShell.Messaging.Convert.ToByteArray(rand));
                    _parentResponse(result);
                    _lastResult = result;
                }
                else
                {
                    Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.Uninitialized, ResultType.None, null);
                    _parentResponse(result);
                    _lastResult = result;
                }
            }
            catch (Exception e)
            {
                Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.InternalException, ResultType.None, Encoding.UTF8.GetBytes(e.ToString()));
                _parentResponse(result);
                _lastResult = result;
            }

            _busySignal.Reset();
            return true;
        }
        #endregion Command Handlers
    }
}
