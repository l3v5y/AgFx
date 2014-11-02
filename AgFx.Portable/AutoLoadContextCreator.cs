using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AgFx
{
    public class AutoLoadContextCreator
    {
        private readonly Dictionary<Type, ConstructorInfo> _loadContextTypes = new Dictionary<Type, ConstructorInfo>();

        public LoadContext CreateLoadContext<T>(object value)
        {
            if (value is LoadContext)
            {
                return (LoadContext)value;
            }

            ConstructorInfo ci;

            bool valueExists = false;

            lock (_loadContextTypes)
            {
                valueExists = _loadContextTypes.TryGetValue(typeof(T), out ci);
            }

            if (!valueExists)
            {

                bool hasLoadContextProperty = false;

                /*
                var loadContextProps = from pi in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                       where typeof(LoadContext).IsAssignableFrom(pi.PropertyType) && pi.Name == "LoadContext"
                                       select pi;
                */
                // http://dotnet.dzone.com/articles/reflection-winrt
                var loadContextProps = from pi in typeof(T).GetRuntimeProperties()
                                       where typeof(LoadContext).GetTypeInfo().IsAssignableFrom(pi.PropertyType.GetTypeInfo()) && pi.Name == "LoadContext"
                                       select pi;

                foreach (var lcProp in loadContextProps)
                {
                    hasLoadContextProperty = true;
                    Type lcType = lcProp.PropertyType;
                    //Type lcType = lcProp.GetType(); 

                    // check the type's ctor.
                    /*
                    var ctors = from c in lcType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                                where c.GetParameters() != null &&
                                      c.GetParameters().Length == 1 &&
                                      c.GetParameters()[0].ParameterType.IsInstanceOfType(value)
                                select c;
                    */
                    var ctors = from c in lcType.GetTypeInfo().DeclaredConstructors
                                where c.GetParameters() != null &&
                                      c.GetParameters().Length == 1 &&
                                      c.GetParameters()[0].ParameterType.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo())
                                select c;

                    ci = ctors.FirstOrDefault();

                    if (ci != null)
                    {
                        lock (_loadContextTypes)
                        {
                            _loadContextTypes[typeof(T)] = ci;
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
                        _loadContextTypes[typeof(T)] = null;
                    }
                }
                else if (ci == null)
                {
                    throw new ArgumentException("Could not auto create a LoadContext for type.  Make sure the type has a property called LoadContext of the appropriate type, and that it has a public constructor that takes the instance value type" + typeof(T).FullName);
                }
            }

            if (ci != null)
            {
                return (LoadContext)ci.Invoke(new object[] { value });
            }
            else
            {
                return new LoadContext(value);
            }
        }
    }
}
