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
using System.Text;

namespace GMD.STM32F4.CmdShell.Messaging
{
    public static class Convert
    {
        public static byte[] ToByteArray(short fromShort)
        {
            return new byte[] { (byte)((fromShort >> 8) & 0xFF), (byte)((fromShort >> 0) & 0xFF) };
        }

        public static byte[] ToByteArray(int fromInt)
        {
            return new byte[] { 
                                (byte)((fromInt >> 24) & 0xFF),
                                (byte)((fromInt >> 16) & 0xFF),
                                (byte)((fromInt >> 8) & 0xFF),
                                (byte)((fromInt >> 0) & 0xFF)
                              };
        }

        public static byte[] ToByteArray(uint fromUint)
        {
            return new byte[] { 
                                (byte)((fromUint >> 24) & 0xFF),
                                (byte)((fromUint >> 16) & 0xFF),
                                (byte)((fromUint >> 8) & 0xFF),
                                (byte)((fromUint >> 0) & 0xFF)
                              };
        }

        public static byte[] ToByteArray(ulong fromUlong)
        {
            return new byte[] { 
                                (byte)((fromUlong >> 56) & 0xFF),
                                (byte)((fromUlong >> 48) & 0xFF),
                                (byte)((fromUlong >> 40) & 0xFF),
                                (byte)((fromUlong >> 32) & 0xFF),
                                (byte)((fromUlong >> 24) & 0xFF),
                                (byte)((fromUlong >> 16) & 0xFF),
                                (byte)((fromUlong >> 8) & 0xFF),
                                (byte)((fromUlong >> 0) & 0xFF)
                              };
        }

        public static short ToShort(byte[] bytes)
        {
            short hiOrder = (short)((bytes[0] << 8) & 0xFF00);
            short loOrder = (short)((bytes[1] << 0) & 0x00FF);
            return (short)(hiOrder + loOrder);
        }

        public static int ToInt(byte[] bytes)
        {
            return (int)((bytes[0] << 24) & 0xFF000000) + (int)((bytes[1] << 16) & 0x00FF0000) + (int)((bytes[2] << 8) & 0x0000FF00) + (int)((bytes[3] << 0) & 0x000000FF);
        }

        public static uint ToUint(byte[] bytes)
        {
            return (uint)((bytes[0] << 24) & 0xFF000000) + (uint)((bytes[1] << 16) & 0x00FF0000) + (uint)((bytes[2] << 8) & 0x0000FF00) + (uint)((bytes[3] << 0) & 0x000000FF);
        }

        public static ulong ToUlong(byte[] bytes)
        {
            return (ulong)(( ((ulong)(bytes[0] << 56) & 0xFF00000000000000) + ((ulong)(bytes[1] << 48) & 0x00FF000000000000) + ((ulong)(bytes[2] << 40) & 0x0000FF0000000000) + ((ulong)(bytes[2] << 32) & 0x000000FF00000000) + ((ulong)(bytes[4] << 24) & 0x00000000FF000000) + ((ulong)(bytes[5] << 16) & 0x0000000000FF0000) + ((ulong)(bytes[6] << 8) & 0x000000000000FF00) + ((ulong)(bytes[7] << 0) & 0x00000000000000FF)));
        }

        public static string ToBinaryString(UInt32 n)
        {
            return ToBinaryString(n, 0);
        }

        public static string ToBinaryString(UInt32 n, byte delimitSize)
        {
            StringBuilder sb = new StringBuilder(32 + 32 % delimitSize + 1);
            uint remainder;
            bool delimit = delimitSize > 0;
            byte space = 0;
            byte cnt = 0;

            while (n > 0)
            {
                remainder = n % 2;
                n /= 2;
                sb.Insert(0, remainder.ToString(), 1);
                cnt++;
                if (delimit && ++space % delimitSize == 0)
                    sb.Insert(0, "-", 1);
            }

            if (delimit)
                sb.Insert(0, "0", delimitSize - space % delimitSize);
            else
                sb.Insert(0, "0", 32 - cnt);

            return sb.ToString();
        }

        public static string ToBinaryString(byte[] bytes)
        {
            int bitCount = bytes.Length * 8;
            StringBuilder sb = new StringBuilder(bitCount);
            int cnt = 0;

            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                byte n = bytes[i];
                int remainder = 0;

                while (n > 0)
                {
                    remainder = n % 2;
                    n /= 2;
                    sb.Insert(0, remainder.ToString(), 1);
                }
            }

            sb.Insert(0, "0", bitCount - cnt);

            return sb.ToString();
        }
    }

    /// <summary>
    /// Enumeration of result data types
    /// </summary>
    public enum ResultType : byte
    {
        Bitmap = 0, //Bitwise data, such as 01110010, not a .bmp
        ByteArray = 1, //Array of bytes, no interpretation needed
        Short = 2, //Short represented as an array of 2 bytes
        UShort = 3, //UShort represented as an array of 2 bytes
        Int = 4, //Int represented as an array of 4 bytes
        UInt = 5, //UInt represented as an array of 4 bytes
        Byte = 6, 
        Long = 8, //Long represented as an array of 8 bytes
        ULong = 9, //ULong represented as an array of 8 bytes
        String = 10, //String represented as an array of bytes (UTF8)

        None = 255, //No data returned
    }

    /// <summary>
    /// Enumeration of result codes returned by command handlers
    /// </summary>
    public enum ResultCode : byte
    {
        Success = 0,
        Unsupported = 1,
        Timeout = 2,
        ParameterError = 3,
        ParameterMissing = 4,
        Uninitialized = 5,
        OperationPending = 6,

        IncompleteMessage = 253,
        InternalException = 254,
        UnspecifiedFailure = 255
    }

    /// <summary>
    /// Encapsulates the message structure.
    /// </summary>
    /// <remarks>Commands, including parameters and included spaces, are limited to 500 characters</remarks>
    public sealed class Message
    {
        public static byte[] SOM = new byte[] { 0xFE, 0xFF, 0xFE };
        public static byte[] EOM = new byte[] { 0xFF, 0x01, 0xFF };

        public short MessageLength;
        public ulong Sequence;
        public short CallBackID;
        public string Command;
        public string[] Args;

        public Message() { }

        public Message(ulong sequence, short callBackID, string command, string[] args)
        {
            int argLen = 0;
            int argSpaces = 0;
            if ( args != null )
                foreach (string s in args)
                {
                    argLen += Encoding.UTF8.GetBytes(s).Length;
                    argSpaces++;
                }

            MessageLength = (short)(SOM.Length + sizeof(short) + sizeof(ulong) + sizeof(short) + Encoding.UTF8.GetBytes(command).Length + argLen + argSpaces + EOM.Length);
            Sequence = sequence;
            CallBackID = callBackID;
            Command = command;
            Args = args;
        }

        /// <summary>
        /// Convert message to a byte array
        /// </summary>
        /// <returns>Array of bytes composing the message</returns>
        public byte[] ToBytes()
        {
            byte[] retVal = null;

            ArrayList args = new ArrayList();
            byte argByteCount = 0;

            byte[] cmd = Encoding.UTF8.GetBytes(Command);

            if (Args != null)
            {
                foreach (string arg in Args)
                {
                    byte[] argBytes = Encoding.UTF8.GetBytes(arg);
                    args.Add(argBytes);
                    argByteCount += (byte)argBytes.Length;
                }

                //Delimiters
                argByteCount += (byte)Args.Length;
            }

            byte msgLen = (byte)(SOM.Length + + sizeof(short) + sizeof(ulong) + sizeof(short) + cmd.Length + argByteCount + EOM.Length);

            retVal = new byte[msgLen];
            int destIdx = 0;

            Array.Copy(SOM, 0, retVal, 0, SOM.Length);
            destIdx += SOM.Length;
            
            Array.Copy(Convert.ToByteArray(MessageLength), 0, retVal, destIdx, sizeof(short));
            destIdx += sizeof(short);

            Array.Copy(Convert.ToByteArray(Sequence), 0, retVal, destIdx, sizeof(ulong));
            destIdx += sizeof(ulong);

            Array.Copy(Convert.ToByteArray(CallBackID), 0, retVal, destIdx, sizeof(short));
            destIdx += sizeof(short);

            Array.Copy(cmd, 0, retVal, destIdx, cmd.Length);
            destIdx += cmd.Length;

            foreach (byte[] argBytes in args)
            {
                retVal[destIdx++] += 0x20;
                Array.Copy(argBytes, 0, retVal, destIdx, argBytes.Length);
                destIdx += argBytes.Length;
            }

            Array.Copy(EOM, 0, retVal, destIdx, EOM.Length);
            destIdx += EOM.Length;

            return retVal;
        }

        /// <summary>
        /// Converts an appropriate byte array to a message
        /// </summary>
        /// <param name="bytes">Byte stream constructed by or in pattern of Message.ToBytes()</param>
        /// <returns>Message object, or null if error</returns>
        public static Message ToMessage(byte[] bytes)
        {
            Message retVal = null;

            if (bytes[0] != SOM[0] ||
                 bytes[1] != SOM[1] ||
                 bytes[2] != SOM[2] ||
                 bytes[bytes.Length - 3] != EOM[0] ||
                 bytes[bytes.Length - 2] != EOM[1] ||
                 bytes[bytes.Length - 1] != EOM[2])
            {
                return null;
            }

            byte[] lenBytes = new byte[] { bytes[3], bytes[4] };
            byte[] seqBytes = new byte[] { bytes[5], bytes[6], bytes[7], bytes[8], bytes[9], bytes[10], bytes[11], bytes[12] };
            byte[] callbackBytes = new byte[] { bytes[13], bytes[14] };

            short length = Convert.ToShort(lenBytes);
            ulong sequence = Convert.ToUlong(seqBytes);
            short callbackID = Convert.ToShort(callbackBytes);

            if (length != bytes.Length)
                return null;

            string cmdLine = new string(UTF8Encoding.UTF8.GetChars(bytes, 15, bytes.Length - 15 - 3));
            string[] args = null;
            string[] breakdown = cmdLine.Split(' ');

            if (breakdown.Length > 1)
            {
                args = new string[breakdown.Length - 1];
                Array.Copy(breakdown, 1, args, 0, breakdown.Length - 1);
            }

            retVal = new Message(sequence, callbackID, breakdown[0], args);

            return retVal;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Length: " + MessageLength.ToString());
            sb.AppendLine("Sequence: " + Sequence.ToString());
            sb.AppendLine("CallbackID: " + CallBackID.ToString());
            sb.AppendLine("Command: " + Command);
            if (Args != null)
            {
                for (int i = 0; i < Args.Length; i++)
                {
                    sb.AppendLine("Arg " + i.ToString() + ": " + (string)Args[i]);
                }
            }
            else
                sb.AppendLine("No arguments");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Encapsulates command responses
    /// </summary>
    public sealed class Result
    {
        public static byte[] SOM = new byte[] { 0xFE, 0xFF, 0xFE };
        public static byte[] EOM = new byte[] { 0xFF, 0x01, 0xFF };

        public short ResultLength;

        /// <summary>
        /// Sequence of the message to which this is a response
        /// </summary>
        public ulong Sequence; 

        /// <summary>
        /// Caller provided callback ID, simply returned with result
        /// from original message
        /// </summary>
        public short CallBackID;

        /// <summary>
        /// Result of command execution
        /// </summary>
        public ResultCode Resultcode;

        /// <summary>
        /// Type of data returned, if any
        /// </summary>
        public ResultType Resulttype;

        /// <summary>
        /// Any data returned by command execution
        /// </summary>
        public byte[] Data;

        public Result() { }

        public Result(ulong sequence, short callbackID, ResultCode resultcode, ResultType resultType, byte[] data)
        {
            ResultLength = (short)(sizeof(short) + sizeof(ulong) + sizeof(short) + 1 + 1 + (data == null ? 0 : data.Length) + SOM.Length + EOM.Length);
            Sequence = sequence;
            CallBackID = callbackID;
            Resultcode = resultcode;
            Resulttype = resultType;
            Data = data;
        }

        /// <summary>
        /// Convert result to a byte array
        /// </summary>
        /// <returns>Array of bytes composing the message</returns>
        public byte[] ToBytes()
        {
                            //Start      End           Length         Sequence        CallbackID      RC  RT
            int byteCount = SOM.Length + EOM.Length + sizeof(short) + sizeof(ulong) + sizeof(short) + 1 + 1 + (Data == null ? 0 : Data.Length);
            byte[] retVal = new byte[byteCount];

            int i = 0;
            Array.Copy(SOM, 0, retVal, i, SOM.Length);
            i += SOM.Length;

            Array.Copy(Convert.ToByteArray((short)byteCount), 0, retVal, i, sizeof(short));
            i += sizeof(short);

            Array.Copy(Convert.ToByteArray(Sequence), 0, retVal, i, sizeof(ulong));
            i += sizeof(ulong);

            Array.Copy(Convert.ToByteArray(CallBackID), 0, retVal, i, sizeof(short));
            i += sizeof(short);

            retVal[i++] = (byte)Resultcode;

            retVal[i++] = (byte)Resulttype;

            if (Data != null)
            {
                Array.Copy(Data, 0, retVal, i, Data.Length);
                i += Data.Length;
            }

            Array.Copy(EOM, 0, retVal, i, EOM.Length);

            return retVal;
        }

        /// <summary>
        /// Converts an appropriate byte array to a result
        /// </summary>
        /// <param name="bytes">Byte stream constructed by or in pattern of Result.ToBytes()</param>
        /// <returns>Result object, or null if error</returns>
        public static Result ToResult(byte[] bytes)
        {
            Result retVal = null;

            int blen = bytes.Length;

            int sIdx = 0, eIdx = 0;
            bool sFound = false, eFound = false;

            if ( blen < 20 )
                return null;

            while ( !sFound && sIdx + 2 < bytes.Length )
            {
                if (bytes[sIdx] == SOM[0] &&
                    bytes[sIdx + 1] == SOM[1] &&
                    bytes[sIdx + 2] == SOM[2]
                   )
                    sFound = true;
                else
                    sIdx++;
            }

            eIdx = sIdx + 17;
            while (!eFound && eIdx < bytes.Length)
            {
                if (bytes[eIdx] == EOM[0] &&
                    bytes[eIdx + 1] == EOM[1] &&
                    bytes[eIdx + 2] == EOM[2]
                   )
                    eFound = true;
                else
                    eIdx++;
            }

            if (!sFound || !eFound)
                return null;

            byte[] lenBytes = new byte[] { bytes[sIdx + 3], bytes[sIdx + 4] };
            byte[] seqBytes = new byte[] { bytes[sIdx + 5], bytes[sIdx + 6], bytes[sIdx + 7], bytes[sIdx + 8], bytes[sIdx + 9], bytes[sIdx + 10], bytes[sIdx + 11], bytes[sIdx + 12] };
            byte[] callbackBytes = new byte[] { bytes[sIdx + 13], bytes[sIdx + 14] };

            short msgLength = Convert.ToShort(lenBytes);
            ulong sequence = Convert.ToUlong(seqBytes);
            short callbackID = Convert.ToShort(callbackBytes);
            ResultCode resultc = (ResultCode)bytes[sIdx + 15];
            ResultType resultT = (ResultType)bytes[sIdx + 16];

            if (msgLength != eIdx + 3 - sIdx)
                return null;

            byte[] data = null;

            if (eIdx + 3 - sIdx > 17)
            {
                data = new byte[eIdx - sIdx - 17];
                Array.Copy(bytes, 17, data, 0, eIdx - sIdx - 17);
            }

            retVal = new Result(sequence, callbackID, resultc, resultT, data);

            return retVal;
        }

        public string DataString()
        {
            string strData = "";

            if (Data != null)
            {
                switch (Resulttype)
                {
                    case ResultType.Bitmap:
                        strData = Convert.ToBinaryString(Data);
                        break;
                    case ResultType.ByteArray:
                        StringBuilder baSB = new StringBuilder(Data.Length * 3);
                        for (int i = 0; i < Data.Length; i++)
                            baSB.Append(Data[i]);
                        strData = baSB.ToString();
                        break;
                    case ResultType.Byte:
                        strData = Data[0].ToString();
                        break;
                    case ResultType.Short:
                        strData = Convert.ToShort(Data).ToString();
                        break;
                    case ResultType.UShort:
                        strData = ((ushort)Convert.ToShort(Data)).ToString();
                        break;
                    case ResultType.Int:
                        strData = Convert.ToInt(Data).ToString();
                        break;
                    case ResultType.UInt:
                        strData = Convert.ToUint(Data).ToString();
                        break;
                    case ResultType.Long:
                        strData = ((long)Convert.ToUlong(Data)).ToString();
                        break;
                    case ResultType.ULong:
                        strData = Convert.ToUlong(Data).ToString();
                        break;
                    case ResultType.String:
                        strData = new string(Encoding.UTF8.GetChars(Data));
                        break;
                    case ResultType.None:
                        break;
                }
            }

            return strData;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Length: " + ResultLength.ToString());
            sb.AppendLine("Sequence: " + Sequence.ToString());
            sb.AppendLine("CallbackID: " + CallBackID.ToString());
            sb.AppendLine("ResultCode: " + Resultcode.ToString());
            sb.AppendLine("ResultType: " + Resulttype.ToString());
            sb.AppendLine("Data: " + DataString());

            return sb.ToString();
        }
    }

}