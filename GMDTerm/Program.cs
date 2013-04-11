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
// Date Created: 2012-02-16
//
/******************************************************************************/

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

using GMD.STM32F4.CmdShell.Messaging;

namespace GMDTerm
{
    /// <summary>
    /// Simple command line "terminal" for interacting with the 
    /// GMD.STM32F4.AsyncCmdShell program on an STM32F4
    /// </summary>
    class Program
    {
        const int RESPONSE_TIMEOUT =
#if DEBUG
        30000; //Give 30 seconds for a response while debugging
#else
        10000; //Give 10 seconds for a response in release
#endif

        #region Private Members
        private static string _comPort = "COM1";
        private static int _baudRate = 115200;
        private static Parity _parity = Parity.None;
        private static int _dataBits = 8;
        private static StopBits _stopBits = StopBits.One;

        private static ulong _msgSeq = 0;

        private static AutoResetEvent _dataReadySignal;

        private static byte[] readBuffer = new byte[2048];
        private static int bufPointer = 0;

        private static SerialPort _mcuPort = null;
        #endregion Private Members

        static void Main(string[] args)
        {
            if (!ValidateCmdLine(args))
                return;

            _dataReadySignal = new AutoResetEvent(false);

            int j = 0;

            while (!Connect() && j++ < 10)
                Thread.Sleep(500);

            if (j == 10)
            {
                Console.WriteLine("Failed to connect to device. Aborting.");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey(true);
                return;
            }

            string localCommand = "";

            while (true)
            {
                Console.Write("STM32F4>");
                localCommand = Console.ReadLine();

                int preResult = PreprocessCommmand(localCommand);
                if (preResult == 1)
                    break;
                if (preResult == 2)
                    continue;

                Console.WriteLine();

                Message msg = FormatMessage(localCommand);

                ResultCode incMsgResult = ResultCode.IncompleteMessage;
                int retries = 0;

                while (incMsgResult == ResultCode.IncompleteMessage && retries++ < 10)
                {
                    InitializeBuffer();

                    _mcuPort.DiscardInBuffer();
                    _mcuPort.DiscardOutBuffer();

                    SendMessage(msg);

                    if (!_dataReadySignal.WaitOne(RESPONSE_TIMEOUT))
                    {
                        Console.WriteLine("Timeout waiting for response");
                        continue;
                    }
                    else
                    {
                        ReadResponse();

                        Result result = Result.ToResult(readBuffer);

                        if (result != null)
                        {
                            incMsgResult = result.Resultcode;

                            if (result.DataString() != "")
                                Console.WriteLine(result.DataString());
                            else
                                Console.WriteLine(result.Resultcode.ToString());
                        }

                        Console.WriteLine();
                    }
                }
            }
        }

        #region Private Members
        /// <summary>
        /// Preprocess the command for local action
        /// </summary>
        /// <param name="command">Command line to processs</param>
        /// <returns>0 = no action required, 1 = exit command loop, 2 = exit current iteration of command loop</returns>
        private static int PreprocessCommmand(string command)
        {
            int retVal = 0;

            switch (command.ToLower())
            {
                case "exit":
                case "close":
                case "quit":
                    retVal = 1;
                    break;
                case "disconnect":
                    Disconnect();
                    retVal = 2;
                    break;
                case "connect":
                    Connect();
                    retVal = 2;
                    break;
                case "":
                    retVal = 2;
                    break;
                default:
                    retVal = 0;
                    break;
            }

            return retVal;
        }

        private static bool Connect()
        {
            if (InitializePort())
            {
                _mcuPort.DiscardInBuffer();
                _mcuPort.DiscardOutBuffer();

                if (SendCmdNoParams("ping"))
                {
                    Console.WriteLine("Device attached...");

                    string cmd = "date " + DateTime.Now.ToString("MM/dd/yyyy");
                    Console.WriteLine("Synchronizing date...");
                    if (SendCmdNoParams(cmd))
                    {
                        Console.WriteLine("Synchronizing time...");
                        cmd = "time " + DateTime.Now.ToString("HH:mm:ss");
                        SendCmdNoParams(cmd);

                        Console.WriteLine("Connection complete");
                    }
                    else
                        return false;

                    return true;
                }
                else
                    Console.WriteLine("Failed to attach to device");
            }

            return false;
        }

        private static void Disconnect()
        {
            DeinitPort();
        }

        private static bool SendCmdNoParams(string command)
        {
            Message msg = FormatMessage(command);
            ulong sequence = msg.Sequence;

            SendMessage(msg);

            if (!_dataReadySignal.WaitOne(RESPONSE_TIMEOUT))
            {
                return false;
            }
            else
            {
                ReadResponse();

                Result result = Result.ToResult(readBuffer);

                InitializeBuffer();

                if (result != null && result.Resultcode == ResultCode.Success && result.Sequence == sequence)
                    return true;
            }

            return false;
        }

        private static void SendMessage(Message msg)
        {
            byte[] msgBytes = msg.ToBytes();

            try { _mcuPort.Write(msgBytes, 0, msgBytes.Length); }
            catch (Exception e) { Console.WriteLine("Exception: " + e.ToString()); }
        }

        private static void ReadResponse()
        {
            while (_mcuPort.BytesToRead > 0 || bufPointer < 5)
            {
                int maxRead = readBuffer.Length - bufPointer;

                bufPointer += _mcuPort.Read(readBuffer, bufPointer, _mcuPort.BytesToRead > maxRead ? maxRead : _mcuPort.BytesToRead);

                if (bufPointer < 5) 
                    Thread.Sleep(20);
            }

            short msgLen = GMD.STM32F4.CmdShell.Messaging.Convert.ToShort(new byte[] { readBuffer[3], readBuffer[4] });

            while (bufPointer < msgLen)
            {
                bufPointer += _mcuPort.Read(readBuffer, bufPointer, _mcuPort.BytesToRead);

                if (bufPointer < msgLen)
                    Thread.Sleep(20);
            }
        }

        private static void DeinitPort()
        {
            if (_mcuPort != null)
            {
                _mcuPort.Close();
                _mcuPort.DataReceived -= new SerialDataReceivedEventHandler(mcuPort_DataReceived);
                _mcuPort = null;
            }
        }

        private static bool InitializePort()
        {
            DeinitPort();
            
            _mcuPort = new SerialPort(_comPort, _baudRate, _parity, _dataBits, _stopBits);
            _mcuPort.DataReceived += new SerialDataReceivedEventHandler(mcuPort_DataReceived);

            try
            {
                _mcuPort.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to open port: " + e.Message);
                return false;
            }

            Console.WriteLine("Port to device " + (_mcuPort.IsOpen ? "is" : "failed to") + " open...");

            return _mcuPort.IsOpen;
        }

        private static void InitializeBuffer()
        {
            for (int i = 0; i < readBuffer.Length; i++)
                readBuffer[i] = 0;

            bufPointer = 0;
        }

        private static void mcuPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_mcuPort.BytesToRead > 0)
                _dataReadySignal.Set();
        }
        
        /// <summary>
        /// Format the command line
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private static Message FormatMessage(string command)
        {
            string[] args = null;
            string cmd = command;

            if ( command.IndexOf(" ") > 0 )
            {
                string[] cmdSplit = command.Split(' ');
                args = new string[cmdSplit.Length - 1];
                Array.Copy(cmdSplit, 1, args, 0, cmdSplit.Length - 1);
                cmd = cmdSplit[0];
            }

            return new Message(++_msgSeq, (short)0, cmd, args);
        }

        /// <summary>
        /// Format the response
        /// </summary>
        /// <param name="resp">Response message bytes</param>
        /// <returns>Result object to string</returns>
        private static string FormatResponse(byte[] resp)
        {
            Result rslt = Result.ToResult(resp);

            if (rslt == null)
                return "";
            else
                return rslt.ToString();
        }

        private static void PrintHelp()
        {
            Console.WriteLine(
@"Usage:
    GMDTerm
or 
    GMDTerm <Com Port> <BaudRate> <Parity> <Data Bits> <Stop Bits>
where
    <Com Port> = [COM1|COM2|COM3|...] Default = COM1
    <BaudRate> = [300|600|1200|2400|4800|9600|...] Default = 115200
    <Parity>   = [N|E|O|S|M] Default = N
    <Data Bits>= [7|8] Default = 8
    <Stop Bits>= [0|1|1.5|2] Default = 1
");
        }

        private static bool ValidateCmdLine(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0].ToLower() == "help" || args[0].ToLower() == "?")
                {
                    PrintHelp();
                    Console.Write("Press any key.");
                    Console.ReadKey(true);
                    return false;
                }

                if (args.Length != 5)
                {
                    PrintHelp();
                    Console.Write("Press any key.");
                    Console.ReadKey(true);
                    return false;
                }

                if (!ParseCmdLine(args))
                {
                    PrintHelp();
                    Console.Write("Press any key.");
                    Console.ReadKey(true);
                    return false;
                }
            }

            return true;
        }

        private static bool ParseCmdLine(string[] args)
        {
            if (args[0].IndexOf("COM") == 0)
                _comPort = args[0];
            else
                return false;

            try
            {
                _baudRate = System.Convert.ToInt32(args[1], 10);
            }
            catch
            {
                return false;
            }

            switch (args[2])
            {
                case "N":
                    _parity = Parity.None;
                    break;
                case "E":
                    _parity = Parity.Even;
                    break;
                case "O":
                    _parity = Parity.Odd;
                    break;
                case "S":
                    _parity = Parity.Space;
                    break;
                case "M":
                    _parity = Parity.Mark;
                    break;
                default:
                    return false;
            }

            try
            {
                _dataBits = System.Convert.ToInt32(args[3], 10);
            }
            catch
            {
                return false;
            }

            switch (args[4])
            {
                case "0":
                    _stopBits = StopBits.None;
                    break;
                case "1":
                    _stopBits = StopBits.One;
                    break;
                case "1.5":
                    _stopBits = StopBits.OnePointFive;
                    break;
                case "2":
                    _stopBits = StopBits.Two;
                    break;
                default:
                    return false;
            }

            return true;
        }
        #endregion Private Members
    }
}
