using System;
using System.Threading;
using SQLite.Net.Platform.WindowsPhone8;
using Xunit;

namespace AgFx.Test
{
    public class SQLiteStoreProviderTests
    {
        private const string DatabaseName = "test.db";
        private const string CacheItemInfoName = "TEST";

        [Fact]
        public void TestDelete()
        {
            var storeProvider = new SQLiteStoreProvider(new SQLitePlatformWP8(), "test.db");

            storeProvider.Delete(CacheItemInfoName);

            var cacheItemInfo = storeProvider.GetItem(CacheItemInfoName);

            Assert.Null(cacheItemInfo);

            var cii = new CacheItemInfo(CacheItemInfoName, DateTime.Now, DateTime.Now.AddHours(1));

            storeProvider.Write(cii, new byte[] {1});
            Thread.Sleep(100); // let the write happen;

            cacheItemInfo = storeProvider.GetItem(CacheItemInfoName);

            Assert.NotNull(cacheItemInfo);

            storeProvider.Delete(cii);

            Thread.Sleep(100);

            cacheItemInfo = storeProvider.GetItem(CacheItemInfoName);

            Assert.Null(cacheItemInfo);
        }

        [Fact]
        public void TestWriteAndRead()
        {
            var storeProvider = new SQLiteStoreProvider(new SQLitePlatformWP8(), DatabaseName);
            storeProvider.Delete();

            var cacheItemInfo = storeProvider.GetItem(CacheItemInfoName);

            Assert.Null(cacheItemInfo);

            var cii = new CacheItemInfo(CacheItemInfoName, DateTime.Now, DateTime.Now.AddHours(1));

            storeProvider.Write(cii, new byte[] {7});
            Thread.Sleep(200); // let the write happen;

            var bytes = storeProvider.Read(cii);

            Assert.NotNull(bytes);
            Assert.Equal(1, bytes.Length);
            Assert.Equal(7, bytes[0]);

            // cleanup
            storeProvider.Delete(cii);
        }
    }
}