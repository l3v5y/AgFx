// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AgFx
{
    public static class ReflectionHelpers
    {
        /// <summary>
        /// Determines whether the specified object is an instance of the current Type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="o">The object to compare with the current type.</param>
        /// <returns>true if the current Type is in the inheritance hierarchy of the 
        /// object represented by o, or if the current Type is an interface that o 
        /// supports. false if neither of these conditions is the case, or if o is 
        /// null, or if the current Type is an open generic type (that is, 
        /// ContainsGenericParameters returns true).</returns>
        public static bool IsInstanceOfType(this Type type, object o)
        {
            return o != null && type.IsAssignableFrom(o.GetType());
        }

        /// <summary>
        /// Checks if this type is assignable from the given type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        public static bool IsAssignableFrom(this Type type, Type targetType)
        {
            if (targetType == null)
            {
                return false;
            }

            if (type == targetType || type == typeof(object))
            {
                return true;
            }

            if (targetType.GetTypeInfo().IsSubclassOf(targetType))
            {
                return true;
            }

            if (type.GetTypeInfo().IsInterface)
            {
                return targetType.ImplementsInterface(type);
            }

            if (type.IsGenericParameter)
            {
                var genericParameterConstraints = type.GetTypeInfo().GetGenericParameterConstraints();
                for (int i = 0; i < genericParameterConstraints.Length; i++)
                {
                    if (!genericParameterConstraints[i].IsAssignableFrom(targetType))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Walks the inheritance tree to see if type, or any base types implement the given interface
        /// </summary>
        /// <param name="type"></param>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static bool ImplementsInterface(this Type type, Type interfaceType)
        {
            while (type != null)
            {
                var interfaces = type.GetTypeInfo().ImplementedInterfaces.ToArray();
                if (interfaces != null)
                {
                    for (int i = 0; i < interfaces.Length; i++)
                    {
                        if (interfaces[i] == interfaceType || (interfaces[i] != null && interfaces[i].ImplementsInterface(interfaceType)))
                        {
                            return true;
                        }
                    }
                }
                type = type.GetTypeInfo().BaseType;
            }
            return false;
        }

        /// <summary>
        /// Checks if a given type is an enum
        /// </summary>
        /// <param name="type"></param>
        /// <returns>True if type is an enum</returns>
        public static bool IsEnum(this Type type)
        {
            return type.GetTypeInfo().IsEnum;
        }

        /// <summary>
        /// Walks through constructors of the given type, finding one with no parameters, or null
        /// </summary>
        /// <param name="type"></param>
        /// <returns>Default, parameterless constructor or null</returns>
        public static ConstructorInfo GetDefaultConstructor(this Type type)
        {
            var ctors = from c in type.GetTypeInfo().DeclaredConstructors
                        where c.GetParameters() == null ||
                              c.GetParameters().Length == 0
                        select c;

            return ctors.FirstOrDefault();
        }

        // TODO: Comment
        // TODO: Test
        public static IEnumerable<PropertyInfo> GetReadWriteProperties(this object obj)
        {
            return from p in obj.GetType().GetRuntimeProperties()
                   where p.CanRead && p.CanWrite
                   select p;
        }
    }
}
