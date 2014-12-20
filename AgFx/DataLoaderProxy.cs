// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace AgFx
{
    /// <summary>
    ///     This is a helper class to access the methods on an IDataLoader object.  Since
    ///     .NET doesn't support variance an "is" check won't work - we have to walk the interfaces
    /// </summary>
    internal static class DataLoaderProxy
    {
        private static readonly Dictionary<Type, MethodInfo> GetLoadRequestCache = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> DeserializeCache = new Dictionary<Type, MethodInfo>();

        public static LoadRequest GetLoadRequest(object dataLoader, LoadContext loadContext, Type objectType)
        {
            var dataLoaderType = dataLoader.GetType();

            MethodInfo mi;

            if (!GetLoadRequestCache.TryGetValue(dataLoaderType, out mi))
            {
                mi = FindMethodWorker(dataLoaderType, "GetLoadRequest", typeof (DataLoaderProxy), typeof (Type));

                GetLoadRequestCache[dataLoaderType] = mi;
            }

            try
            {
                return (LoadRequest) mi.Invoke(dataLoader, new object[] {loadContext, objectType});
            }
            catch (TargetInvocationException t)
            {
                throw t.InnerException;
            }
        }

        // TODO: DO we need type/dataloader etc?
        public static object Deserialize(object dataLoader, LoadContext loadContext,
            Type objectType, Stream stream)
        {
            var dataLoaderType = dataLoader.GetType();
            MethodInfo mi;

            if (!DeserializeCache.TryGetValue(dataLoaderType, out mi))
            {
                mi = FindMethodWorker(dataLoaderType, "Deserialize", typeof (DataLoaderProxy), typeof (Type),
                    typeof (Stream));
                DeserializeCache[dataLoaderType] = mi;
            }

            try
            {
                return mi.Invoke(dataLoader, new object[] {loadContext, objectType, stream });
            }
            catch (TargetInvocationException t)
            {
                throw t.InnerException;
            }
        }

        /// <summary>
        ///     This does the heavy lifting of finding the interface and then getting the right types off of it.
        /// </summary>
        /// <param name="dataLoaderType"></param>
        /// <param name="methodName"></param>
        /// <param name="paramTypes">
        ///     The types of the parameters.  We pass typeof(DataLoaderProxy) as a
        ///     marker to substitute in the generic argument type.
        /// </param>
        /// <returns></returns>
        private static MethodInfo FindMethodWorker(Type dataLoaderType, string methodName, params Type[] paramTypes)
        {
            // get the type of the attribute.
            //
            var dataLoaderInterface = (from i in dataLoaderType.GetInterfaces()
                where i.IsGenericType && i.GetGenericTypeDefinition() == typeof (IDataLoader<>)
                select i).First();

            var contextType = dataLoaderInterface.GetGenericArguments()[0];

            for (var i = 0; i < paramTypes.Length; i++)
            {
                if (paramTypes[i] == typeof (DataLoaderProxy))
                {
                    paramTypes[i] = contextType;
                }
            }

            var mi = dataLoaderType.GetMethod(methodName, paramTypes);
            Debug.Assert(mi != null, String.Format("Couldn't find {0} on {1}", methodName, dataLoaderType.FullName));
            return mi;
        }
    }
}