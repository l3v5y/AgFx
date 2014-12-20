using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AgFx
{
    internal class AutoLoadContextCreator
    {
        // TODO: Do we need to cache CIs - quantify perf benefits/complexity trade off
        private readonly Dictionary<Type, ConstructorInfo> _loadContextTypes = new Dictionary<Type, ConstructorInfo>();

        // TODO: Can this be made static
        /// <summary>
        /// Create or get a load context for the given object
        /// </summary>
        /// <typeparam name="T">Type of the model to create a LoadContext for</typeparam>
        /// <param name="value">Can be a LoadContext, a ModelItem or an object for a new LoadContext identity</param>
        /// <returns>Either the load context of an existing item, or a new LoadContext</returns>
        public LoadContext CreateLoadContext<T>(object value)
        {
            var loadContext = value as LoadContext;
            if (loadContext != null)
            {
                return loadContext;
            }

            var modelItemBase = value as ModelItemBase;
            if (modelItemBase != null)
            {
                return modelItemBase.LoadContext;
            }

            ConstructorInfo ci;

            bool valueExists;

            lock (_loadContextTypes)
            {
                valueExists = _loadContextTypes.TryGetValue(typeof (T), out ci);
            }

            if (!valueExists)
            {
                var hasLoadContextProperty = false;
                var loadContextProps = from pi in typeof (T).GetRuntimeProperties()
                    where
                        typeof (LoadContext).GetTypeInfo().IsAssignableFrom(pi.PropertyType.GetTypeInfo()) &&
                        pi.Name == "LoadContext"
                    select pi;

                foreach (var lcProp in loadContextProps)
                {
                    hasLoadContextProperty = true;
                    var lcType = lcProp.PropertyType;
                    var ctors = from c in lcType.GetTypeInfo().DeclaredConstructors
                        where c.GetParameters().Length == 1 &&
                              c.GetParameters()[0].ParameterType.GetTypeInfo()
                                  .IsAssignableFrom(value.GetType().GetTypeInfo())
                        select c;

                    ci = ctors.FirstOrDefault();

                    if (ci != null)
                    {
                        lock (_loadContextTypes)
                        {
                            _loadContextTypes[typeof (T)] = ci;
                        }
                        break;
                    }
                }

                if (!hasLoadContextProperty)
                {
                    // this is a poco, just return the default thing.
                    //
                    lock (_loadContextTypes)
                    {
                        _loadContextTypes[typeof (T)] = null;
                    }
                }
                else if (ci == null)
                {
                    throw new ArgumentException(
                        "Could not auto create a LoadContext for type.  Make sure the type has a property called LoadContext of the appropriate type, and that it has a public constructor that takes the instance value type" +
                        typeof (T).FullName);
                }
            }

            if (ci != null)
            {
                return (LoadContext) ci.Invoke(new[] {value});
            }
            return new LoadContext(value);
        }
    }
}