using System;
using System.IO;
using System.Threading.Tasks;

namespace AgFx
{
    public interface ICacheEntry : ILoadContextItem
    {
        // TODO: Comment this up a bit
        object DeserializeAction(LoadContext id, Stream data, bool isOptimized);
        Func<object, Stream, bool> SerializeOptimizedDataAction { get; set; }

        UpdateCompletionHandler NextCompletedAction { get; }

        Type ObjectType { get; set; }

        CachePolicy CachePolicy { get; }

        object GetValue(bool cacheOnly);

        void SetForRefresh();
        Task ClearAsync();
        object GetDataLoader();

        bool CheckIfAnyoneCares();

        EntryStats Stats { get; set; }
        object ValueInternal { get; set; }

        void UpdateValue(object instance, LoadContext loadContext);
        bool SerializeDataToCache(object value, DateTime updateTime, DateTime? expirationTime, bool optimizedOnly);
        Task<object> LoadFromCache();
        string UniqueName { get; }
    }
}
