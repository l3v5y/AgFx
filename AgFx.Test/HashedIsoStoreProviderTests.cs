using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;
using AgFx.IsoStore;
using Xunit;

namespace AgFx.Test
{
    public class HashedIsoStoreProviderTests : IDisposable
    {
        private const string CacheDirectoryName = "«c";

        public void Dispose()
        {
            DeleteDirectoryRecursively(IsolatedStorageFile.GetUserStoreForApplication(), CacheDirectoryName);
        }

        private static IEnumerable<string> GetIsoStoreFiles(string root = CacheDirectoryName)
        {
            var isoStore = IsolatedStorageFile.GetUserStoreForApplication();
            var search = Path.Combine(root, "*");
            var files = new List<string>();
            if(!isoStore.DirectoryExists(root))
            {
                return files;
            }
            files.AddRange(isoStore.GetFileNames(search));

            foreach(var d in isoStore.GetDirectoryNames(search))
            {
                files.AddRange(GetIsoStoreFiles(Path.Combine(root, d)));
            }
            return files;
        }

        private static bool CompareFileLists(IEnumerable<string> before, IEnumerable<string> after)
        {
            before = before.ToList();
            after = after.ToList();
            if(before.Count() != after.Count())
            {
                return false;
            }

            before = before.OrderBy(s => s, StringComparer.InvariantCulture);
            after = after.OrderBy(s => s, StringComparer.InvariantCulture);

            for(var i = 0; i < before.Count(); i++)
            {
                if(!String.Equals(before.ElementAt(i), after.ElementAt(i)))
                {
                    return false;
                }
            }
            return true;
        }

        [Fact]
        public void TestDelete()
        {
            var storeProvider = new HashedIsoStoreProvider(CacheDirectoryName);
            var files = GetIsoStoreFiles();

            storeProvider.Delete();
            storeProvider.Delete("KillMe");

            var cacheItemInfo = storeProvider.GetItem("KillMe");

            Assert.Null(cacheItemInfo);

            var cii = new CacheItemInfo("KillMe", DateTime.Now, DateTime.Now.AddHours(1));

            storeProvider.Write(cii, new byte[] {1});
            Thread.Sleep(100); // let the write happen;

            cacheItemInfo = storeProvider.GetItem("KillMe");

            Assert.NotNull(cacheItemInfo);

            storeProvider.Delete(cii);

            Thread.Sleep(100);

            cacheItemInfo = storeProvider.GetItem("KillMe");

            Assert.Null(cacheItemInfo);

            var newFiles = GetIsoStoreFiles();

            Assert.True(CompareFileLists(files, newFiles));
        }

        [Fact]
        public void TestWriteAndReadWithoutFlush()
        {
            var storeProvider = new HashedIsoStoreProvider(CacheDirectoryName);

            var cacheItemInfo = storeProvider.GetItem("KillMe");

            Assert.Null(cacheItemInfo);

            var cii = new CacheItemInfo("KillMe", DateTime.Now, DateTime.Now.AddHours(1));

            storeProvider.Write(cii, new byte[] {7});
            Thread.Sleep(200); // let the write happen;

            var bytes = storeProvider.Read(cii);

            Assert.NotNull(bytes);
            Assert.Equal(1, bytes.Length);
            Assert.Equal(7, bytes[0]);

            // cleanup
            storeProvider.Delete(cii);
        }

        [Fact]
        public void TestWriteAndReadWithFlush()
        {
            var storeProvider = new HashedIsoStoreProvider(CacheDirectoryName);

            var files = GetIsoStoreFiles();

            var cacheItemInfo = storeProvider.GetItem("KillMe");

            Assert.Null(cacheItemInfo);

            var cii = new CacheItemInfo("KillMe", DateTime.Now, DateTime.Now.AddHours(1));

            storeProvider.Write(cii, new byte[] {7});
            Thread.Sleep(100); // let the write happen;

            var newFiles = GetIsoStoreFiles();

            Assert.False(CompareFileLists(files, newFiles));

            var bytes = storeProvider.Read(cii);

            Assert.NotNull(bytes);
            Assert.Equal(1, bytes.Length);
            Assert.Equal(7, bytes[0]);

            storeProvider.Delete(cii);
        }

        private static void DeleteDirectoryRecursively(IsolatedStorageFile storageFile, string dirName)
        {
            var pattern = dirName + @"\*";
            var files = storageFile.GetFileNames(pattern);
            foreach(var fName in files)
            {
                storageFile.DeleteFile(Path.Combine(dirName, fName));
            }
            var dirs = storageFile.GetDirectoryNames(pattern);
            foreach(var dName in dirs)
            {
                DeleteDirectoryRecursively(storageFile, Path.Combine(dirName, dName));
            }
            storageFile.DeleteDirectory(dirName);
        }
    }
}