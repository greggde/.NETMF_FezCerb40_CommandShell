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

namespace GMD.STM32F4.CmdShell
{
    /// <summary>
    /// Provides interface for GPIO on the STM32F4 based MCUs, targeted for the
    /// FEZ Cerb40.  "Left side pins"* are inputs with the Pull Up/Down resistor
    /// disabled.  "Right side pins"* are outputs with Glitch Filter off.  See
    /// code for precise pin lists.
    /// 
    /// </summary>
    public class IOCmdHandler : IMessageHandler
    {
        #region Private Members
        private Result _lastResult = null;
        private ManualResetEvent _busySignal;
        private CmdHandlerHelpProc _parentHelp;
        private CmdHandlerResponseProc _parentResponse;
        private CmdHandlerProc _parentUnsolicitedMsgProc;

        private InputPort[] _Input;
        private OutputPort[] _Output;
        #endregion Private Members

        #region Constants
        public const string INPUTPINS =
@"Input Pin  0 = PB5
Input Pin  1 = PB6
Input Pin  2 = PB7
Input Pin  3 = PB8  
Input Pin  4 = PC12
Input Pin  5 = PD2
Input Pin  6 = PB3
Input Pin  7 = PB9
Input Pin  8 = PB4
Input Pin  9 = PC11
Input Pin 10 = PA14
Input Pin 11 = PC10
Input Pin 12 = PA6
Input Pin 13 = PA7
Input Pin 14 = PA13
Input Pin 15 = PA8
";
        public const string OUTPUTPINS =
@"Output Pin  0 = PC0
Output Pin  1 = PC1
Output Pin  2 = PC2
Output Pin  3 = PC3  
Output Pin  4 = PA0
Output Pin  5 = PA1
Output Pin  6 = PA4
Output Pin  7 = PA5
Output Pin  8 = PB10
Output Pin  9 = PB11
Output Pin 10 = PB14
Output Pin 11 = PB15
Output Pin 12 = PC6
Output Pin 13 = PC7
Output Pin 14 = PC8
Output Pin 15 = PC9
";
        #endregion Constants

        public IOCmdHandler()
        {
            _busySignal = new ManualResetEvent(false);

            _Input = new InputPort[] {
                new InputPort(Pin.PB5, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PB6, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PB7, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PB8, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PC12, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PD2, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PB3, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PB9, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PB4, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PC11, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PA14, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PC10, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PA6, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PA7, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PA13, false, Port.ResistorMode.Disabled),
                new InputPort(Pin.PA8, false, Port.ResistorMode.Disabled)
            };

            _Output = new OutputPort[] {
//                new OutputPort(Pin.PC0, false),
                new OutputPort(Pin.PC1, false),
                new OutputPort(Pin.PC2, false),
                new OutputPort(Pin.PC3, false),
                new OutputPort(Pin.PA0, false),
                new OutputPort(Pin.PA1, false),
                new OutputPort(Pin.PA4, false),
                new OutputPort(Pin.PA5, false),
                new OutputPort(Pin.PB10, false),
                new OutputPort(Pin.PB11, false),
                new OutputPort(Pin.PB14, false),
                new OutputPort(Pin.PB15, false),
                new OutputPort(Pin.PC6, false),
                new OutputPort(Pin.PC7, false),
                new OutputPort(Pin.PC8, false),
                new OutputPort(Pin.PC9, false)
            };
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
            CmdHandlerDef[] retVal = new CmdHandlerDef[7];

            retVal[0] = new CmdHandlerDef("read", new CmdHandlerProc(CmdHandler_Read), "Read all input states 0 - 15");
            retVal[1] = new CmdHandlerDef("set", new CmdHandlerProc(CmdHandler_Set), "Set all output states 0 - 15. Param: 16 char bit string, e.g. 1110100111010110");
            retVal[2] = new CmdHandlerDef("readinput", new CmdHandlerProc(CmdHandler_ReadPin), "Read input state of given pin. Params: [0-15]");
            retVal[3] = new CmdHandlerDef("ri", new CmdHandlerProc(CmdHandler_ReadPin), "Read input state of given pin. Params: [0-15]");
            retVal[4] = new CmdHandlerDef("setoutput", new CmdHandlerProc(CmdHandler_SetPin), "Set output state of given pin. Params: [0-15] [0|1]");
            retVal[5] = new CmdHandlerDef("so", new CmdHandlerProc(CmdHandler_SetPin), "Set output state of given pin. Params: [0-15] [0|1]");
            retVal[6] = new CmdHandlerDef("getpinmap", new CmdHandlerProc(CmdHandler_GetPinMap), "Get mapping of pins. Params: [input|output]");

            return retVal;
        }

        /// <summary>
        /// These are message handlers registered with the parent to handle incoming commands.
        /// </summary>
        /// <returns></returns>
        #region Command Handlers
        public bool CmdHandler_GetPinMap(Message msg)
        {
            _busySignal.Set();

            StringBuilder sbMapping = new StringBuilder();

            if (msg.Args != null && msg.Args.Length > 0)
            {
                if ((msg.Args[0] as string).ToLower() == "input")
                    sbMapping.Append(INPUTPINS);
                else
                    sbMapping.Append(OUTPUTPINS);
            }
            else
            {
                sbMapping.Append(INPUTPINS);
                sbMapping.Append(OUTPUTPINS);
            }

            Result resp = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.String, Encoding.UTF8.GetBytes(sbMapping.ToString()));
            _parentResponse(resp);

            _lastResult = resp;

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Read(Message msg)
        {
            _busySignal.Set();

            try
            {
                ushort reads = 0;

                for (byte i = 0; i < 16; i++)
                {
                    bool pinState = _Input[i].Read();

                    if (pinState)
                        reads |= (ushort)(0x01 << i);
                }

                Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.UShort, new byte[] { (byte)(reads & 0xFF00), (byte)(reads & 0x00FF) });
                _parentResponse(result);
                _lastResult = result;
            }
            catch(Exception e)
            {
                Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.InternalException, ResultType.String, Encoding.UTF8.GetBytes(e.ToString()));
                _parentResponse(result);
                _lastResult = result;
            }

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_Set(Message msg)
        {
            _busySignal.Set();

            try
            {
                if (msg.Args == null || msg.Args.Length == 0)
                {
                    Result errResult = new Result(msg.Sequence, msg.CallBackID, ResultCode.ParameterMissing, ResultType.String, Encoding.UTF8.GetBytes("16 character Bit string, e.g. 111010011101011"));
                    _parentResponse(errResult);
                    _lastResult = errResult;
                    return true;
                }

                if ((msg.Args[0] as string).Length != 16)
                {
                    Result errResult = new Result(msg.Sequence, msg.CallBackID, ResultCode.ParameterError, ResultType.String, Encoding.UTF8.GetBytes("16 character Bit string, e.g. 111010011101011"));
                    _parentResponse(errResult);
                    _lastResult = errResult;
                    return true;
                }

                for (byte i = 0; i < 16; i++)
                {
                    // Zeros are false, all other characters are true
                    bool pinState = !((msg.Args[0] as string)[i]=='0');

                    _Output[i].Write(pinState);
                }

                //For now, just parrot back the settings.  Eventually, we may want to read the states and return that.
                Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.String, Encoding.UTF8.GetBytes(msg.Args[0] as string));
                _parentResponse(result);
                _lastResult = result;
            }
            catch (Exception e)
            {
                Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.InternalException, ResultType.String, Encoding.UTF8.GetBytes(e.ToString()));
                _parentResponse(result);
                _lastResult = result;
            }

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_ReadPin(Message msg)
        {
            _busySignal.Set();

            try
            {
                if (msg.Args == null || msg.Args.Length == 0)
                {
                    Result errResult = new Result(msg.Sequence, msg.CallBackID, ResultCode.ParameterMissing, ResultType.String, Encoding.UTF8.GetBytes("Input Pin Number: 0 - 15"));
                    _parentResponse(errResult);
                    _lastResult = errResult;
                    return true;
                }

                int pinNumber;

                try
                {
                    pinNumber = System.Convert.ToInt32(msg.Args[0] as string);
                }
                catch
                {
                    pinNumber = -1;
                }

                if (pinNumber < 0 || pinNumber > 15)
                {
                    Result errResult = new Result(msg.Sequence, msg.CallBackID, ResultCode.ParameterError, ResultType.String, Encoding.UTF8.GetBytes("Input Pin Number: 0 - 15"));
                    _parentResponse(errResult);
                    _lastResult = errResult;
                    return true;
                }

                bool pinState = _Input[pinNumber].Read();

                Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.Byte, new byte[] { (byte)(pinState ? 1 : 0) });
                _parentResponse(result);
                _lastResult = result;
            }
            catch (Exception e)
            {
                Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.InternalException, ResultType.String, Encoding.UTF8.GetBytes(e.ToString()));
                _parentResponse(result);
                _lastResult = result;
            }

            _busySignal.Reset();
            return true;
        }

        public bool CmdHandler_SetPin(Message msg)
        {
            _busySignal.Set();

            try
            {
                if (msg.Args == null || msg.Args.Length < 2)
                {
                    Result errResult = new Result(msg.Sequence, msg.CallBackID, ResultCode.ParameterMissing, ResultType.String, Encoding.UTF8.GetBytes("Output Pin Number: 0 - 15, State: 0 - 1"));
                    _parentResponse(errResult);
                    _lastResult = errResult;
                    return true;
                }

                int pinNumber;
                bool state;

                try
                {
                    pinNumber = System.Convert.ToInt32(msg.Args[0] as string);
                }
                catch
                {
                    pinNumber = -1;
                }

                if (pinNumber < 0 || pinNumber > 15)
                {
                    Result errResult = new Result(msg.Sequence, msg.CallBackID, ResultCode.ParameterError, ResultType.String, Encoding.UTF8.GetBytes("Output Pin Number: 0 - 15"));
                    _parentResponse(errResult);
                    _lastResult = errResult;
                    return true;
                }

                try
                {
                    state = System.Convert.ToInt32(msg.Args[1] as string) == 0 ? false : true;
                }
                catch
                {
                    Result errResult = new Result(msg.Sequence, msg.CallBackID, ResultCode.ParameterError, ResultType.String, Encoding.UTF8.GetBytes("State: 0 - 1"));
                    _parentResponse(errResult);
                    _lastResult = errResult;
                    return true;
                }

                _Output[pinNumber].Write(state);

                Result resp = new Result(msg.Sequence, msg.CallBackID, ResultCode.Success, ResultType.Byte, new byte[] { (byte)(state ? 1 : 0) });
                _parentResponse(resp);
                _lastResult = resp;
            }
            catch (Exception e)
            {
                Result result = new Result(msg.Sequence, msg.CallBackID, ResultCode.InternalException, ResultType.String, Encoding.UTF8.GetBytes(e.ToString()));
                _parentResponse(result);
                _lastResult = result;
            }

            _busySignal.Reset();
            return true;
        }
        #endregion Command Handlers
    }
}
