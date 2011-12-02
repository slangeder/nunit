﻿// ***********************************************************************
// Copyright (c) 2008 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Reflection;
using System.Collections;
using NUnit.Framework.Extensibility;

namespace NUnit.Framework.Builders
{
    /// <summary>
    /// Provides data from fields marked with the DatapointAttribute or the
    /// DatapointsAttribute.
    /// </summary>
    public class DatapointProvider : IParameterDataProvider
    {
        #region IDataPointProvider Members

        /// <summary>
        /// Determine whether any data is available for a parameter.
        /// </summary>
        /// <param name="parameter">A ParameterInfo representing one
        /// argument to a parameterized test</param>
        /// <returns>
        /// True if any data is available, otherwise false.
        /// </returns>
        public bool HasDataFor(System.Reflection.ParameterInfo parameter)
        {
            Type parameterType = parameter.ParameterType;
            MemberInfo method = parameter.Member;
            Type fixtureType = method.ReflectedType;

            if (!method.IsDefined(typeof(TheoryAttribute), true))
                return false;

            if (parameterType == typeof(bool) || parameterType.IsEnum)
                return true;

            foreach (MemberInfo member in fixtureType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (member.IsDefined(typeof(DatapointAttribute), true) &&
                    GetTypeFromMemberInfo(member) == parameterType)
                        return true;
                else if (member.IsDefined(typeof(DatapointSourceAttribute), true) &&
                    GetElementTypeFromMemberInfo(member) == parameterType)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Return an IEnumerable providing data for use with the
        /// supplied parameter.
        /// </summary>
        /// <param name="parameter">A ParameterInfo representing one
        /// argument to a parameterized test</param>
        /// <returns>
        /// An IEnumerable providing the required data
        /// </returns>
        public System.Collections.IEnumerable GetDataFor(System.Reflection.ParameterInfo parameter)
        {
            ObjectList datapoints = new ObjectList();

            Type parameterType = parameter.ParameterType;
            Type fixtureType = parameter.Member.ReflectedType;

            foreach (MemberInfo member in fixtureType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (member.IsDefined(typeof(DatapointAttribute), true))
                {
                    if (GetTypeFromMemberInfo(member) == parameterType &&
                        member.MemberType == MemberTypes.Field)
                    {
                        FieldInfo field = member as FieldInfo;
                        if (field.IsStatic)
                            datapoints.Add(field.GetValue(null));
                        else
                            datapoints.Add(field.GetValue(ProviderCache.GetInstanceOf(fixtureType)));
                    }
                }
                else if (member.IsDefined(typeof(DatapointSourceAttribute), true))
                {
                    if (GetElementTypeFromMemberInfo(member) == parameterType)
                    {
                        object instance;

                        switch(member.MemberType)
                        {
                            case MemberTypes.Field:
                                FieldInfo field = member as FieldInfo;
                                instance = field.IsStatic ? null : ProviderCache.GetInstanceOf(fixtureType);
                                datapoints.AddRange((ICollection)field.GetValue(instance));
                                break;
                            case MemberTypes.Property:
                                PropertyInfo property = member as PropertyInfo;
                                MethodInfo getMethod = property.GetGetMethod(true);
                                instance = getMethod.IsStatic ? null : ProviderCache.GetInstanceOf(fixtureType);
                                datapoints.AddRange((ICollection)property.GetValue(instance,null));
                                break;
                            case MemberTypes.Method:
                                MethodInfo method = member as MethodInfo;
                                instance = method.IsStatic ? null : ProviderCache.GetInstanceOf(fixtureType);
                                datapoints.AddRange((ICollection)method.Invoke(instance, Type.EmptyTypes));
                                break;
                        }
                    }
                }
            }

            if (datapoints.Count == 0)
            {
                if (parameterType == typeof(bool))
                {
                    datapoints.Add(true);
                    datapoints.Add(false);
                }
                else if (parameterType.IsEnum)
                {
                    datapoints.AddRange(System.Enum.GetValues(parameterType));
                }
            }

            return datapoints;
        }

        private Type GetTypeFromMemberInfo(MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                default:
                    return null;
            }
        }

        private Type GetElementTypeFromMemberInfo(MemberInfo member)
        {
            Type type = GetTypeFromMemberInfo(member);

            if (type == null)
                return null;

            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType && type.Name == "IEnumerable`1")
                return type.GetGenericArguments()[0];

            return null;
        }

        #endregion
    }
}
