Overview:
	The GMD STM32F4 Command Shell, "GMD Command Shell", or simply "Shell" is 
an interface that allows "terminal" access to methods run on the Cerb40.  For
example, type 'help' in the terminal, and a list of commands and their
descriptions will be returned.
	The program should easily port to other boards in the Cerberus family, if
any porting is necessary.  No boards other than the Cerb40 have been tested.
	Rather than accepting raw text commands, the Shell employs a messaging
system that encompasses message metadata as well as the command and any
parameters.  Results are similarly structured.  The included "terminal"
program, GMDTerm, a C# Windows app, wraps commands in messages and unwraps
results and displays the results, if any.  The Shell is designed to use by any
application, not just the terminal.
	The current incarnation is a synchronous implementation, but the Shell does
provide for asynchronous messaging.  GMDTerm does not listen for asynchronous
messages, but the functionality could be incorporated into any app using the 
Shell, including GMDTerm.
	Communication is over UART, or optionally, RS-232, depending upon hardware
setup, using COM2 of the Cerb.  Data rates of 460800 baud have been used without
issue so far.  The UART settings of the Cerb are hardcoded, while GMDTerm takes
command line parameters for the communication setttings.

Hardware:
	It is not essential to use COM2 of the Cerb, any UART will do, as will any
parity, data bit, and stop bit settings.  Simply modify the settings in the
Main() method of Program.cs and start GMDTerm with matching settings.  The
programs were developed using a USB to TTL cable to provide a virtual COM port
on the PC side that can directly connect to the Cerb's TTL UART.  Using a level
converter and using RS-232 connected to a PC serial port is perfectly
acceptable.

Messaging Specifics:
	The messaging system employed by the shell is designed to provide a robust
interface between the Cerb and it's consumer app.  A "terminal" program is 
provided, but the messaging is more robust than a simple command line interface
in that it provides metadata beyond the command line.
	Messages, and Results, are transmitted with specific begin and end tags
to help ensure message completion.  A message length is also included to help
determine message completion.  A sequence number is included in the message to
identify it, useful for detecting duplicated messages, and this sequence number
is returned in the result to identify the originating message.  Additionally, a
callback ID in the message allows the caller to identify the handler for the
result, as the result will also contain the callback ID.  The rest of the 
message payload consists of the command and any parameters.
	Results are similarly structured in that they contain a length, sequence
number, and callback ID.  Additionally, they contain a ResultCode, indicating
the outcome of the call, e.g. Success.  Results also contain a ResultType to
indicate the type of data being returned.  See MessagingObjects.cs in the
project GMD.STM32F4.CmdShell.Messaging for a list of data types supported in
this first release.  Finally, Results may contain data of the type designated by
the ResultType byte.

Command Handlers:
	A command handler is a class that implements the IMessageHandler interface.
This interface defines the methods used to interact with the main Shell methods
for handling commands from GMDTerm, or any other app written to connect to the
Shell.  An IMessageHandler provides one or more CmdHandlerDef objects to the 
parent, defining the commands handled, the method that handles it, and a text
help string that is returned when the "help" command is received.  See the file
IMessageHandler.cs in GMD.STM32F4.CmdShell.Messaging for more detailed info, or
see one of the objects below for an example of implementation.
	Three command handlers are provided in this release.  They provide examples
of system commands, I/O commands, and Random Number Generator commands.

	SysCmdHandler:
	Provides "system" commands, and is therefore included in the main Shell
	executable.  It provides for the following commands:

		ping - request an acknowledgement from the Shell
		help (also ? or h) - provide help for all commands, or a specific one
							 if provided as a parameter
		version (also ver) - return the Shell version
		time - return the Shell time, or set it if given as a parameter
		date - return the Shell date, or set it if given as a parameter
		info - display system info
		shutdown - shutdown the Shell application
		reboot - reboot the device

	GPIO Handler:
	Provides commands for controlling GPIO on the Cerb, currently 16 digital in
	and 16 digital out:

		read - return all input states
		set - set all output states (requires parameter)
		readinput (also ri) - return the state of a specific input pin
		setoutput (also so) - set the state of a specific output pin
		getpinmap - get a mapping of pin numbers to device pins

	RNG Handler:
	Provides command to retrieve a random number generated by the hardware RNG
	on the Cerb. It currently has one command:
		
		getrnd - return a random UInt

	Custom Handlers:
	Commands and handlers can be defined for almost any operation.  Operations
	need not be synchronous.  As long as the consumer app is prepared for the
	asynchronous result, results can be passed back periodically, on an event,
	an interrupt, a completion, or any other trigger.  For example, a result
	could be sent in response to an digital input signal on the Cerb, or when
	an analog signal reaches a certain threshold.  Operations can be on-demand
	or long running.  Custom handlers allow the developer to have real-time
	interaction with their device either through a terminal or programatically
	with ease.

	Terminal Commands:
	The following commands are intercepted by GMDTerm and are not passed on
	to the device:
		
		exit (also close or quit) - close the terminal
		disconnect - close the port to the device
		connect - open and initialize the connection to the device

Notes:
	Currently, PC0 is not available for output, it is being used for a
	diagnostic LED to provide feedback when not attached to the debugger.

	The device does not seem to receive data after a reboot without the 
	debugger connected.  Running under the debugger works, and data 
	continues to flow after disconnecting the debugger and even the 
	debugging USB cable, but rebooting outside the debugger seems to 
	leave the device unable to listen on COM2.


Future enhancements:
	Add USB communications support
	Add support for Analog input/output to GPIO Handler.
	Handle clock and other errors in RNG
	Better handling of message reads

Package:
	Solution and Project GMD.STM32F4.CmdShell - The Shell, to install on the
		Cerb device.  Approximately 54k deployable including provided message
		handlers.
	Project GMD.STM32F4.CmdShell.Messaging - Contains the messaging objects and
		Command Handler interface.
	Project GMD.STM32F4.CmdShell.IO - GPIO Command Handler
	Project GMD.STM32F4.CmdShell.RNG - RNG Command Handler
	Project GMD.STM32F4.Hardware - Hardware addresses, IRQs, Peripherals,
		Registers and Pins of the STM32F4 (written for the Discovery board)
	Project GMD.STM32F4.RNG - An interface to the hardware RNG on the STM32F4

	Solution and Project GMDTerm - .NET "Terminal" program to interact with
		the Shell textually.
