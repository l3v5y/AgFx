using System.Collections.Generic;
using System.Linq;
using SQLite.Net;
using SQLite.Net.Interop;

namespace AgFx
{
    public class SQLiteStoreProvider : IStoreProvider
    {
        private readonly SQLiteConnection _sqliteConnection;

        public SQLiteStoreProvider(ISQLitePlatform sqLitePlatform, string databaseName)
        {
            _sqliteConnection = new SQLiteConnection(sqLitePlatform, databaseName);
            _sqliteConnection.CreateTable<SQLCacheItemInfo>();
            _sqliteConnection.CreateTable<SQLiteStoreItem>();
        }

        public void Delete()
        {
            _sqliteConnection.DropTable<SQLCacheItemInfo>();
            _sqliteConnection.CreateTable<SQLCacheItemInfo>();

            _sqliteConnection.DropTable<SQLiteStoreItem>();
            _sqliteConnection.CreateTable<SQLiteStoreItem>();
        }

        public CacheItemInfo GetItem(string uniqueName)
        {
            var sqlCacheItemInfo =
                _sqliteConnection.Table<SQLCacheItemInfo>().FirstOrDefault(v => v.UniqueName == uniqueName);
            return sqlCacheItemInfo == null ? null : sqlCacheItemInfo.ToCacheItemInfo();
        }

        public IEnumerable<CacheItemInfo> GetItems()
        {
            return _sqliteConnection.Table<SQLCacheItemInfo>().Select(x => x.ToCacheItemInfo());
        }

        public void Delete(CacheItemInfo item)
        {
            Delete(item.UniqueName);
        }

        public void Delete(string uniqueName)
        {
            _sqliteConnection.Delete<SQLCacheItemInfo>(uniqueName);
        }

        public byte[] Read(CacheItemInfo item)
        {
            var storeItem =
                _sqliteConnection.Table<SQLiteStoreItem>().FirstOrDefault(x => x.UniqueName == item.UniqueName);

            return storeItem == null ? null : storeItem.Payload;
        }

        public void Write(CacheItemInfo info, byte[] data)
        {
            var sqlCacheItemInfo = SQLCacheItemInfo.FromCacheItemInfo(info);
            _sqliteConnection.InsertOrReplace(sqlCacheItemInfo);

            var storeItem = new SQLiteStoreItem {UniqueName = info.UniqueName, Payload = data};
            _sqliteConnection.InsertOrReplace(storeItem);
        }
    }
}