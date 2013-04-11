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

using GMD.STM32F4.CmdShell.Messaging;
using Microsoft.SPOT;


namespace GMD.STM32F4.CmdShell
{
    /// <summary>
    /// Delegate for handling commands
    /// </summary>
    /// <param name="msg">Message to be handled</param>
    /// <returns>Whether or not the message was handled. Does not indicate success or failure of command</returns>
    /// <remarks>CmdHandlerProc's return a boolean. This is forward looking; at some point it would be nice to have a chain of command handlers, i.e. handler procs in more than one handler class. A proc returning true indicates the message is handled, false indicates the next in the chain must process.</remarks>
    public delegate bool CmdHandlerProc(Message msg);

    /// <summary>
    /// Retrieve the help text for a given command
    /// </summary>
    /// <param name="cmd">Command for which help is provided</param>
    /// <returns>Help text</returns>
    public delegate string CmdHandlerHelpProc(string cmd);

    /// <summary>
    /// Used to send response back to remote user asynchronously
    /// </summary>
    /// <param name="resp">Result object indicating callback id, result code, and return value, if any</param>
    public delegate void CmdHandlerResponseProc(Result resp);

    /// <summary>
    /// Used to pass a command up to the parent, no parameters
    /// </summary>
    /// <param name="cmd">Command actioned on parent</param>
    public delegate void CmdHandlerParentHandlerProc(string cmd);

    /// <summary>
    /// Structure containing a Command, it's handler proc, and help text
    /// </summary>
    public struct CmdHandlerDef
    {
        public string Cmd;
        public CmdHandlerProc CmdProc;
        public string HelpString;

        public CmdHandlerDef(string cmd, CmdHandlerProc cmdProc, string helpString)
        {
            Cmd = cmd;
            CmdProc = cmdProc;
            HelpString = helpString;
        }
    }

    /// <summary>
    /// Defines the message handler interface.
    /// IMessageHandler's must adhere to this interface. It provides start/stop functionality,
    /// as well as basic state information.  The parent uses methods in this interface to
    /// retrieve the list of command handlers.  The parent also provides callbacks to the methods
    /// needed to return parent help, send responses and unsolicited messages, and command the
    /// parent.
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// Retrieves an array of command handler definitions.
        /// </summary>
        /// <returns>Array of CmdHandlerDef</returns>
        CmdHandlerDef[] GetCmdHandlers();
        
        /// <summary>
        /// Start command handler.  Command handler initiates whatever activity is needed to do
        /// it's job, be it instantiate objects, threads, I/O, etc.
        /// </summary>
        void Start();
        
        /// <summary>
        /// Stop command handler.  Command handler should cease operations and release resources.
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Indicates command handler is ready to accept commands.  Command handler should return
        /// false if the handler is not started, if initialization is not complete, or if other
        /// activities prevent initiation of a new command.
        /// </summary>
        bool ReadyForCommand { get; }

        /// <summary>
        /// Forward looking functionality.  Returns the result of the last command handled.
        /// </summary>
        Result LastResult { get; }

        /// <summary>
        /// Used to provide the command handler with a callback to the parent to retrieve help information.
        /// </summary>
        /// <param name="parentHelpProc">CmdHandlerHelpProc method "pointer" of the parent</param>
        void RegisterParentHelpHandler(CmdHandlerHelpProc parentHelpProc);

        /// <summary>
        /// Used to provide the command handler with a callback to the parent to return a response
        /// to a command.
        /// </summary>
        /// <param name="parentResponsProc">CmdHandlerResponseProc method "pointer" of the parent</param>
        void RegisterParentResponseHandler(CmdHandlerResponseProc parentResponsProc);

        /// <summary>
        /// Used to provide the command handler with a callback to the parent to send an unsolicited
        /// message.  This is forwared looking, and may be used, for example, for Interrupt ports.
        /// </summary>
        /// <param name="parentUnsolicitedMsgProc">CmdHandlerProc method "pointer" of the parent</param>
        void RegisterParentUnsolicitedMsgHandler(CmdHandlerProc parentUnsolicitedMsgProc);

        /// <summary>
        /// Used to provide the command handler with a callback to the parent to command the parent.
        /// This is used, for example, to handle the shutdown command, where the SysCmdHandler must 
        /// tell it's parent to exit.
        /// </summary>
        /// <param name="parentHandlerProc">CmdHandlerParentHandlerProc method "pointer" of the parent</param>
        void RegisterParentHandler(CmdHandlerParentHandlerProc parentHandlerProc);

        void UnregisterParentHelpHandler(CmdHandlerHelpProc parentHelpProc);

        void UnregisterParentResponseHandler(CmdHandlerResponseProc parentResponsProc);

        void UnregisterParentUnsolicitedMsgHandler(CmdHandlerProc parentUnsolicitedMsgProc);

        void UnregisterParentHandler(CmdHandlerParentHandlerProc parentHandlerProc);
    }
}
