using System;
using SQLite.Net.Attributes;

namespace AgFx
{
    // ReSharper disable once InconsistentNaming
    internal class SQLCacheItemInfo
    {
        [PrimaryKey]
        public string UniqueName { get; set; }

        public DateTime UpdatedTime { get; set; }
        public DateTime ExpirationTime { get; set; }
        public bool IsOptimized { get; set; }

        public CacheItemInfo ToCacheItemInfo()
        {
            var cacheItemInfo = new CacheItemInfo(UniqueName)
            {
                UpdatedTime = UpdatedTime,
                ExpirationTime = ExpirationTime,
                IsOptimized = IsOptimized
            };
            return cacheItemInfo;
        }

        public static SQLCacheItemInfo FromCacheItemInfo(CacheItemInfo cacheItemInfo)
        {
            var sqlCacheItemInfo = new SQLCacheItemInfo
            {
                UniqueName = cacheItemInfo.UniqueName,
                UpdatedTime = cacheItemInfo.UpdatedTime,
                ExpirationTime = cacheItemInfo.ExpirationTime,
                IsOptimized = cacheItemInfo.IsOptimized
            };
            return sqlCacheItemInfo;
        }
    }
}