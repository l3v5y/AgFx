using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using PCLStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgFx.Test
{
    [TestClass]
    public class StoreProviderTests
    {
        StoreProviderBase _storeProvider;

        private IEnumerable<string> GetIsoStoreFiles()
        {
            var getIsoStoreTaskFiles = GetFileNamesRecursive(AgFx.PortableHashedStorageProvider.CacheDirectoryPrefix);
            getIsoStoreTaskFiles.Wait();
            return getIsoStoreTaskFiles.Result;
        }

        private async Task<IEnumerable<string>> GetFileNamesRecursive(string root)
        {
           var fileNames = new List<string>();

            if(await FileSystem.Current.LocalStorage.CheckExistsAsync(root) != ExistenceCheckResult.FolderExists)
            {
                return fileNames;
            }

            var rootFolder = await FileSystem.Current.LocalStorage.GetFolderAsync(root);
            var filesInFolder = await rootFolder.GetFilesAsync();
            
            fileNames.AddRange(filesInFolder.Select(file => file.Path));

            var foldersInRootFolder = await rootFolder.GetFoldersAsync();

            foreach (var folder in foldersInRootFolder)
            {
                fileNames.AddRange(await GetFileNamesRecursive(PortablePath.Combine(root, folder.Name)));
            }
            return fileNames;        
        }

        private bool CompareFileLists(IEnumerable<string> before, IEnumerable<string> after)
        {
            if (before.Count() != after.Count())
            {
                return false;
            }

            before = before.OrderBy(s => s, StringComparer.InvariantCulture);
            after = after.OrderBy(s => s, StringComparer.InvariantCulture);

            for (int i = 0; i < before.Count(); i++)
            {
                if (!String.Equals(before.ElementAt(i), after.ElementAt(i)))
                {
                    return false;
                }
            }
            return true;
        }

        [TestInitialize]
        public void InitTests()
        {
            _storeProvider = DataManager.StoreProvider;
        }

        [TestMethod]
        public void TestFlush()
        {
            _storeProvider.Flush(true);
            var files = GetIsoStoreFiles();

            CacheItemInfo cii = new CacheItemInfo("KillMeSoon", DateTime.Now, DateTime.Now.AddHours(1));

            _storeProvider.Write(cii, new byte[] { 2 });
            Thread.Sleep(100); // let the write happen;

            IEnumerable<string> files2 = null;

            if (_storeProvider.IsBuffered)
            {
                files2 = GetIsoStoreFiles();
                Assert.IsTrue(CompareFileLists(files, files2));

                _storeProvider.Flush(true);

            }
            files2 = GetIsoStoreFiles();
            Assert.IsFalse(CompareFileLists(files, files2));

            _storeProvider.Delete(cii);
            _storeProvider.Flush(true);
        }


        [TestMethod]
        public void TestDelete()
        {
            _storeProvider.Flush(true);

            var files = GetIsoStoreFiles();

            _storeProvider.DeleteAll("KillMe");

            var items = _storeProvider.GetItems("KillMe");

            Assert.AreEqual(0, items.Count());

            CacheItemInfo cii = new CacheItemInfo("KillMe", DateTime.Now, DateTime.Now.AddHours(1));

            _storeProvider.Write(cii, new byte[] { 1 });
            Thread.Sleep(100); // let the write happen;

            items = _storeProvider.GetItems("KillMe");

            Assert.AreEqual(1, items.Count());

            _storeProvider.Delete(cii);

            Thread.Sleep(100);

            items = _storeProvider.GetItems("KillMe");

            Assert.AreEqual(0, items.Count());

            _storeProvider.Flush(true);

            var newFiles = GetIsoStoreFiles();

            Assert.IsTrue(CompareFileLists(files, newFiles));
        }

        [TestMethod]
        public void TestWriteAndReadWithoutFlush()
        {
            var items = _storeProvider.GetItems("KillMe");

            Assert.AreEqual(0, items.Count());

            CacheItemInfo cii = new CacheItemInfo("KillMe", DateTime.Now, DateTime.Now.AddHours(1));

            _storeProvider.Write(cii, new byte[] { 7 });
            Thread.Sleep(100); // let the write happen;

            var bytes = _storeProvider.Read(cii);

            Assert.IsNotNull(bytes);
            Assert.AreEqual(1, bytes.Length);
            Assert.AreEqual(7, bytes[0]);

            // cleanup
            _storeProvider.Delete(cii);
        }

        [TestMethod]
        public void TestWriteAndReadWithFlush()
        {
            _storeProvider.Flush(true);
            var files = GetIsoStoreFiles();

            var items = _storeProvider.GetItems("KillMe");

            Assert.AreEqual(0, items.Count());

            CacheItemInfo cii = new CacheItemInfo("KillMe", DateTime.Now, DateTime.Now.AddHours(1));

            _storeProvider.Write(cii, new byte[] { 7 });
            Thread.Sleep(100); // let the write happen;

            _storeProvider.Flush(true);

            var newFiles = GetIsoStoreFiles();

            Assert.IsFalse(CompareFileLists(files, newFiles));

            var bytes = _storeProvider.Read(cii);

            Assert.IsNotNull(bytes);
            Assert.AreEqual(1, bytes.Length);
            Assert.AreEqual(7, bytes[0]);

            _storeProvider.Delete(cii);
            _storeProvider.Flush(true);
        }
    }
}