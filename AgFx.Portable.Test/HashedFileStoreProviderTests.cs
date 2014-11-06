// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using AgFx.HashedFileStore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCLStorage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgFx.Test
{
    [TestClass]
    public class HashedFileStoreTests
    {
        [TestInitialize]
        [TestCleanup]
        public void ClearAllFiles()
        {
            Task.Run(async () =>
            {
                var folders = await FileSystem.Current.LocalStorage.GetFoldersAsync();
                var folderDeletionTasks = new List<Task>();
                foreach (var folder in folders)
                {
                    folderDeletionTasks.Add(folder.DeleteAsync());
                }
                await Task.WhenAll(folderDeletionTasks);
            }).Wait();
        }

        [TestMethod]
        public async Task CanWriteAndReadBackCacheItemInfo()
        {
            var storeProvider = new HashedFileStoreProvider();
            var cacheItemInfo = new CacheItemInfo("TEST", DateTime.Now, DateTime.Now.AddHours(1));

            await storeProvider.WriteAsync(cacheItemInfo, new byte[] { 7 });

            var bytes = await storeProvider.ReadAsync(cacheItemInfo);

            Assert.IsNotNull(bytes);
            Assert.AreEqual(1, bytes.Length);
            Assert.AreEqual(7, bytes[0]);
        }

        [TestMethod]
        public async Task CanWriteAndReadMultipleCacheItemInfo()
        {
            var storeProvider = new HashedFileStoreProvider();
            var cacheItemInfo = new CacheItemInfo("TEST", DateTime.Now, DateTime.Now.AddHours(1));
            var cacheItemInfo2 = new CacheItemInfo("TEST2", DateTime.Now, DateTime.Now.AddHours(1));

            await storeProvider.WriteAsync(cacheItemInfo, new byte[] { 1 });
            await storeProvider.WriteAsync(cacheItemInfo2, new byte[] { 2 });

            var bytes = await storeProvider.ReadAsync(cacheItemInfo);

            Assert.IsNotNull(bytes);
            Assert.AreEqual(1, bytes.Length);
            Assert.AreEqual(1, bytes[0]);

            var bytes2 = await storeProvider.ReadAsync(cacheItemInfo2);

            Assert.IsNotNull(bytes2);
            Assert.AreEqual(1, bytes2.Length);
            Assert.AreEqual(2, bytes2[0]);
        }

        [TestMethod]
        public async Task CanWriteAndDeleteItem()
        {
            var storeProvider = new HashedFileStoreProvider();
            var cacheItemInfo = new CacheItemInfo("TEST", DateTime.Now, DateTime.Now.AddHours(1));

            await storeProvider.WriteAsync(cacheItemInfo, new byte[] { 7 });

            await storeProvider.DeleteAsync(cacheItemInfo);

            var item = await storeProvider.GetItemAsync("TEST");

            Assert.IsNull(item);
        }

        [TestMethod]
        public async Task CanDeleteMultipleFiles()
        {
            var storeProvider = new HashedFileStoreProvider();
            var cacheItemInfo = new CacheItemInfo("TEST", DateTime.Now, DateTime.Now.AddHours(1));
            var cacheItemInfo2 = new CacheItemInfo("TEST2", DateTime.Now, DateTime.Now.AddHours(1));

            await storeProvider.WriteAsync(cacheItemInfo, new byte[] { 1 });
            await storeProvider.WriteAsync(cacheItemInfo2, new byte[] { 2 });

            await storeProvider.ClearAsync();

            var item = await storeProvider.GetItemAsync(cacheItemInfo.UniqueName);

            var item2 = await storeProvider.GetItemAsync(cacheItemInfo2.UniqueName);

            Assert.IsNull(item);
            Assert.IsNull(item2);
        }


        [TestMethod]
        public async Task DeleteAsync_TwoCacheFiles_DeletesOnlyExpected()
        {
            var storeProvider = new HashedFileStoreProvider();
            var cacheItemInfo = new CacheItemInfo("TEST", DateTime.Now, DateTime.Now.AddHours(1));
            var cacheItemInfo2 = new CacheItemInfo("TEST2", DateTime.Now, DateTime.Now.AddHours(1));

            await storeProvider.WriteAsync(cacheItemInfo, new byte[] { 1 });
            await storeProvider.WriteAsync(cacheItemInfo2, new byte[] { 2 });

            await storeProvider.DeleteAsync(cacheItemInfo);

            var item = await storeProvider.GetItemAsync(cacheItemInfo.UniqueName);

            var item2 = await storeProvider.GetItemAsync(cacheItemInfo2.UniqueName);

            Assert.IsNull(item);
            Assert.AreEqual(cacheItemInfo2, item2);
        }

        [TestMethod]
        public async Task DeleteAllAsync_TwoCacheFiles_DeletesAllOfType()
        {
            var storeProvider = new HashedFileStoreProvider();
            var cacheItemInfo = new CacheItemInfo("TEST", DateTime.Now, DateTime.Now.AddHours(1));
            var cacheItemInfo2 = new CacheItemInfo("TEST", DateTime.Now, DateTime.Now.AddHours(1));

            await storeProvider.WriteAsync(cacheItemInfo, new byte[] { 1 });
            await storeProvider.WriteAsync(cacheItemInfo2, new byte[] { 2 });

            await storeProvider.DeleteAsync(cacheItemInfo);

            var item = await storeProvider.GetItemAsync(cacheItemInfo.UniqueName);

            var item2 = await storeProvider.GetItemAsync(cacheItemInfo2.UniqueName);

            Assert.IsNull(item);
            Assert.IsNull(item2);
        }

        [TestMethod]
        public async Task GetItemAsync_ReturnsMostRecentWrite()
        {
            var storeProvider = new HashedFileStoreProvider();
            var cacheItemInfo = new CacheItemInfo("TEST", DateTime.Now.AddHours(-2), DateTime.Now.AddHours(-1));
            var cacheItemInfo2 = new CacheItemInfo("TEST", DateTime.Now, DateTime.Now.AddHours(1));

            await storeProvider.WriteAsync(cacheItemInfo, new byte[] { 1 });
            await storeProvider.WriteAsync(cacheItemInfo2, new byte[] { 2 });

            var item = await storeProvider.GetItemAsync(cacheItemInfo.UniqueName);
            var item2 = await storeProvider.GetItemAsync(cacheItemInfo2.UniqueName);

            Assert.AreEqual(cacheItemInfo2, item);
            Assert.AreEqual(cacheItemInfo2, item2);
        }
    }
}