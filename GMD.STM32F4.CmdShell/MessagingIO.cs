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
using System.IO.Ports;
using System.Text;
using System.Threading;

using GMD.STM32F4.CmdShell.Messaging;
using Microsoft.SPOT;

namespace GMD.STM32F4.CmdShell
{
    /// <summary>
    /// Notifies listeners of an incoming message to be handled
    /// </summary>
    /// <param name="msg">Message to be handled</param>
    public delegate void MessageEventDelegate(Message msg);

    /// <summary>
    /// Notifies listeners of an outgoing response
    /// </summary>
    /// <param name="rslt"></param>
    public delegate void ResultEventDelegate(Result rslt);

    /// <summary>
    /// Notifies listeners of an alert or exception
    /// </summary>
    /// <param name="alert">Alert message</param>
    /// <param name="e">Exception that occurred, if any</param>
    public delegate void AlertDelegate(string alert, Exception e);

    /// <summary>
    /// Notifies listeners of amount of data received
    /// </summary>
    /// <param name="byteCount">Number of bytes received</param>
    public delegate void DataReceivedDelegate(int byteCount);

    /// <summary>
    /// Connection to host PC
    /// </summary>
    public class MessagingIO
    {
        private SerialPort _port;
        private byte[] readBuffer = new byte[512];
        private int readBufferPointer = 0;
        private System.Threading.Timer recvTimer;
        private ManualResetEvent _giveUp = new ManualResetEvent(false);

        public MessagingIO(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            try { _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits); }
            catch (Exception e)
            {
                string msg = "Exception creating " + portName + ": " + e.Message + "-" + e.StackTrace; 
                Debug.Print(msg);

                if (AlertNotify != null)
                {
                    AlertNotify(msg, e);
                }
            }

            recvTimer = new Timer(new System.Threading.TimerCallback(recvTimerCallback), null, 0, 0);

            InitializeBuffer();
        }

        #region Public Interface Methods/Events
        public event MessageEventDelegate MessageReceived;
        public event ResultEventDelegate ResultSent;
        public event AlertDelegate AlertNotify;
        public event DataReceivedDelegate DataReceived;

        public void Start()
        {
            _port.Open();
            _port.DataReceived += new SerialDataReceivedEventHandler(_port_DataReceived);
        }

        public void Stop()
        {
            _port.DataReceived -= new SerialDataReceivedEventHandler(_port_DataReceived);
            _port.Close();
        }

        public ResultCode SendAsynchronousResult(Result result)
        {
            ResultCode retVal = ResultCode.UnspecifiedFailure;

            if (_port.IsOpen)
            {
                byte[] bytes = result.ToBytes();
                _port.Write(bytes, 0, bytes.Length);

                retVal = ResultCode.Success;
            }

            return retVal;
        }

        public ResultCode SendUnsolicitedMessage(Message msg)
        {
            ResultCode retVal = ResultCode.UnspecifiedFailure;

            if (_port.IsOpen)
            {
                byte[] bytes = msg.ToBytes();
                _port.Write(bytes, 0, bytes.Length);

                retVal = ResultCode.Success;
            }

            return retVal;
        }
        #endregion Public Interface Methods/Events

        #region Private Methods
        private void InitializeBuffer()
        {
            for (short i = 0; i < readBuffer.Length; i++)
                readBuffer[i] = 0x00;

            readBufferPointer = 0;
        }

        private void recvTimerCallback(object state)
        {
            _giveUp.Set();
        }

        private void _port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
#if DEBUG
            if (DataReceived != null)
                DataReceived(500);

            Debug.Print("Data received on COM port");
#endif
            lock (readBuffer)
            {
                Thread.Sleep(20);
                int bytesRead = _port.Read(readBuffer, readBufferPointer, (_port.BytesToRead + readBufferPointer > readBuffer.Length) ? readBuffer.Length - readBufferPointer : _port.BytesToRead);
                readBufferPointer += bytesRead;
#if DEBUG
                Debug.Print(DateTime.Now.ToUniversalTime() + ": Initially Read " + bytesRead.ToString() + " bytes");
                int initialRead = bytesRead;
                for(int i = 0; i < readBufferPointer; i++)
                {
                    Debug.Print(readBuffer[i].ToString());
                }
#endif
                recvTimer.Change(1000, 0);

                while (readBufferPointer > 0 && readBufferPointer < 19 && !_giveUp.WaitOne(0, false))
                {
                    bytesRead += _port.Read(readBuffer, readBufferPointer, (_port.BytesToRead + readBufferPointer > readBuffer.Length) ? readBuffer.Length - readBufferPointer : _port.BytesToRead);
                    readBufferPointer += bytesRead;
#if DEBUG
                    Debug.Print(DateTime.Now.ToUniversalTime() + ": Continuing, Read " + (bytesRead - initialRead).ToString() + " bytes");
                    for (int i = initialRead; i < readBufferPointer; i++)
                    {
                        Debug.Print(readBuffer[i].ToString());
                    }
#endif
                }

                if (DataReceived != null)
                    DataReceived(bytesRead);

                _giveUp.Reset();
                recvTimer.Change(Timeout.Infinite, Timeout.Infinite);

                bool sFound = false;
                short idx = 0;
                int msgLen = 0;

                while (!sFound && idx + 2 < readBufferPointer)
                {
                    if (readBuffer[idx] == Message.SOM[0] && readBuffer[idx + 1] == Message.SOM[1] && readBuffer[idx + 2] == Message.SOM[2])
                        sFound = true;
                    else
                        idx++;
                }

                if (sFound)
                {
                    msgLen = Messaging.Convert.ToShort(new byte[] { readBuffer[idx + 3], readBuffer[idx + 4] });

                    while (readBufferPointer - idx < msgLen)
                    {
                        bytesRead += _port.Read(readBuffer, readBufferPointer, (_port.BytesToRead + readBufferPointer > readBuffer.Length) ? readBuffer.Length - readBufferPointer : _port.BytesToRead);
                        readBufferPointer += bytesRead;
                    }
                }

                Message msg = null;

                if (sFound)
                    DecodeMessage(out msg);

                if (MessageReceived != null && msg != null)
                    MessageReceived(msg);

                if (sFound && msg == null)
                {
                    ulong sequence = 0;

                    //If we found the start of a message, but can't decode it
                    //Alert the sender, with the sequence number if possible
                    if (readBufferPointer - idx > 14)
                    {
                        byte[] sequenceBytes = new byte[8];
                        Array.Copy(readBuffer, idx + 5, sequenceBytes, 0, 8);
                        sequence = Messaging.Convert.ToUlong(sequenceBytes);
                    }

                    Result incResult = new Result(sequence, (short)0, ResultCode.IncompleteMessage, ResultType.None, null);

                    if (_port.IsOpen)
                    {
                        byte[] bytes = incResult.ToBytes();
                        _port.Write(bytes, 0, bytes.Length);
                    }
                }

                if (!sFound && readBufferPointer - idx > msgLen)
                {
                    byte[] tmpBytes = new byte[readBufferPointer - idx - msgLen];

                    Array.Copy(readBuffer, idx + msgLen, tmpBytes, 0, readBufferPointer - idx - msgLen);
                    InitializeBuffer();
                    Array.Copy(tmpBytes, 0, readBuffer, 0, tmpBytes.Length);
                    readBufferPointer += tmpBytes.Length;
                }
                else
                    InitializeBuffer();
            }
        }

        private bool DecodeMessage(out Message msg)
        {
            msg = null;

            bool sFound = false;
            bool eFound = false; 
            short idx = 0;
            short idxEnd = 0;

            try
            {
                while (!sFound && idx < readBuffer.Length - 17) //Why 17? Because we will need that many to have a complete message.
                {
                    if (readBuffer[idx] == Message.SOM[0] && readBuffer[idx + 1] == Message.SOM[1] && readBuffer[idx + 2] == Message.SOM[2])
                        sFound = true;
                    else
                        idx++;
                }

                idxEnd = idx;
                idxEnd += 3;

                if (sFound && idx < readBuffer.Length - 10) //Why 10? Because we will need that many to have a complete message.
                {
                    while (!eFound && idxEnd < readBuffer.Length - 2)
                    {
                        if (readBuffer[idxEnd] == Message.EOM[0] && readBuffer[idxEnd + 1] == Message.EOM[1] && readBuffer[idxEnd + 2] == Message.EOM[2])
                            eFound = true;
                        else
                            idxEnd++;
                    }
                }

                if (sFound && eFound) //We have a start and an end
                {
                    byte[] msgBytes = new byte[idxEnd + 3 - idx];
                    Array.Copy(readBuffer, idx, msgBytes, 0, idxEnd + 3 - idx);

                    msg = Message.ToMessage(msgBytes);

                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.Print(e.Message + e.StackTrace);
            }

            return false;
        }
        #endregion
    }
}