using System.Collections.Generic;
using System.Linq;

namespace AgFx.Test
{
    internal class InMemoryStoreProvider : IStoreProvider
    {
        private readonly Dictionary<CacheItemInfo, byte[]> _store;

        public InMemoryStoreProvider()
        {
            _store = new Dictionary<CacheItemInfo, byte[]>();
        }

        public void Delete()
        {
            _store.Clear();
        }

        public IEnumerable<CacheItemInfo> GetItems()
        {
            return _store.Keys;
        }

        public CacheItemInfo GetItem(string uniqueName)
        {
            return _store.Keys.FirstOrDefault(cacheItemInfo => cacheItemInfo.UniqueName == uniqueName);
        }

        public void Delete(CacheItemInfo item)
        {
            _store.Remove(item);
        }

        public void Delete(string uniqueName)
        {
            var matchedItems = _store.Keys.Where(cacheItemInfo => cacheItemInfo.UniqueName == uniqueName);
            foreach(var cacheItemInfo in matchedItems)
            {
                _store.Remove(cacheItemInfo);
            }
        }

        public byte[] Read(CacheItemInfo item)
        {
            return _store.ContainsKey(item) ? _store[item] : null;
        }

        public void Write(CacheItemInfo info, byte[] data)
        {
            _store[info] = (byte[])data.Clone();
        }
    }
}