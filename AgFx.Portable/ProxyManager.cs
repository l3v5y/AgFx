// TODO: Licenses

using System;
using System.Collections.Generic;
using System.Linq;

namespace AgFx
{
    internal class ProxyManager
    {
        internal class ProxyEntry
        {
            public WeakReference ProxyReference { get; set; }
            public Type ObjectType { get; set; }
            public LoadContext LoadContext { get; set; }
            public Action UpdateAction { get; set; }
            public bool UseAsInitialValue { get; set; }
        }

        private static Dictionary<LoadContext, List<ProxyEntry>> _proxies = new Dictionary<LoadContext, List<ProxyEntry>>();

        internal static void CleanupProxies()
        {
            var deadProxies = from pel in _proxies.Values
                              from p in pel
                              where p.ProxyReference != null && !p.ProxyReference.IsAlive
                              select p;

            foreach (var dp in deadProxies.ToArray())
            {
                RemoveProxy(dp);
            }
        }

        internal static void AddProxy(ProxyEntry pe)
        {
            List<ProxyEntry> proxyList;

            if (!_proxies.TryGetValue(pe.LoadContext, out proxyList))
            {
                proxyList = new List<ProxyEntry>();
                _proxies[pe.LoadContext] = proxyList;
            }

            var existingProxy = from p in proxyList
                                where p.ProxyReference.Target == pe.ProxyReference.Target
                                select p;

            if (!existingProxy.Any())
            {
                proxyList.Add(pe);
            }
        }

        internal static IEnumerable<ProxyEntry> GetProxies<T>(LoadContext lc)
        {
            return GetProxies(lc, typeof(T));
        }

        internal static IEnumerable<ProxyEntry> GetProxies(LoadContext lc, Type type)
        {
            List<ProxyEntry> proxyList;

            if (!_proxies.TryGetValue(lc, out proxyList))
            {
                return new ProxyEntry[0];
            }

            var proxies = from p in proxyList
                          where p.ObjectType.IsAssignableFrom(type) && p.ProxyReference.IsAlive
                          select p;

            return proxies.ToArray();
        }

        internal static void RemoveProxy(ProxyEntry pe)
        {
            List<ProxyEntry> proxyList;

            if (_proxies.TryGetValue(pe.LoadContext, out proxyList))
            {
                proxyList.Remove(pe);
            }
        }

        internal static void RemoveProxy(object value)
        {
            var proxy = from pel in _proxies.Values
                        from p in pel
                        where p.ProxyReference != null && p.ProxyReference.Target == value
                        select p;

            foreach (var p in proxy)
            {
                RemoveProxy(p);
            }
        }
    }
}