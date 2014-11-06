// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using PCLStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgFx.HashedFileStore
{
    public class HashedFileStoreProvider : IStoreProvider
    {
        internal const char FileNameSeparator = '»';
        internal const string CacheFolderName = "«c";

        IDictionary<string, CacheItemInfo> _cache = new Dictionary<string, CacheItemInfo>();

        private IFolder Folder
        {
            get
            {
                return FileSystem.Current.LocalStorage;
            }
        }

        // TODO: Refactor to take a predicate, i.e. Date<FixedDate
        public async Task ClearAsync()
        {
            var items = (await GetItemsAsync()).ToArray();
            var tasks = new List<Task>();
            foreach (var item in items)
            {
                tasks.Add(DeleteAsync(item));
            }
            await Task.WhenAll(tasks.ToArray());
        }

        public async Task<CacheItemInfo> GetItemAsync(string uniqueName)
        {
            CacheItemInfo item = null;
            var dir = HashedFileItem.FolderHash(uniqueName);

            if (await Folder.CheckExistsAsync(dir) == ExistenceCheckResult.FolderExists)
            {
                var files = await GetFileNamesRecursive(dir);
                List<CacheItemInfo> items = new List<CacheItemInfo>();
                foreach (var f in files)
                {
                    CacheItemInfo cii = HashedFileItem.FromFileName(f);

                    if (cii != null)
                    {
                        items.Add(cii);
                    }
                }

                var orderedItems = from i in items
                                   where i.UniqueName == uniqueName
                                   orderby i.ExpirationTime descending
                                   select i;

                if (orderedItems.Any())
                {
                    item = orderedItems.First();
                    // TODO: The deletion tasks don't need to happen now, queue them up for execution later
                    foreach (var i in orderedItems.Skip(1))
                    {
                        // TODO: Disable warnings
                        DeleteAsync(i);
                    }
                }

                if (item != null)
                {
                    _cache[uniqueName] = item;
                }
            }
            return item;
        }

        public async Task<IEnumerable<CacheItemInfo>> GetItemsAsync()
        {
            var items = new List<CacheItemInfo>();

            var fileNames = await GetFileNamesRecursive(CacheFolderName);
            items = fileNames.Select(f => new HashedFileItem(f).Item).ToList();

            return items;
        }

        public async Task DeleteAsync(CacheItemInfo item)
        {
            CacheItemInfo cachedItem;

            lock (_cache)
            {
                if (_cache.TryGetValue(item.UniqueName, out cachedItem) && Object.Equals(item, cachedItem))
                {
                    _cache.Remove(item.UniqueName);
                }
            }

            var fi = new HashedFileItem(item);

            var fileName = fi.FileName;

            var file = await Folder.GetFileAsync(fileName);
            await file.DeleteAsync();
        }

        public async Task DeleteAllAsync(string uniqueName)
        {
            lock (_cache)
            {
                if (_cache.ContainsKey(uniqueName))
                {
                    _cache.Remove(uniqueName);
                }
            }

            var dir = HashedFileItem.FolderHash(uniqueName);

            var exists = await Folder.CheckExistsAsync(dir);
            if (exists == ExistenceCheckResult.FolderExists)
            {
                var folder = await Folder.GetFolderAsync(dir);
                var files = await folder.GetFilesAsync();
                await Task.WhenAll(files.Select(f => f.DeleteAsync()));
            }
        }

        public async Task<byte[]> ReadAsync(CacheItemInfo item)
        {
            var fi = new HashedFileItem(item);
            byte[] bytes = null;

            var fileExists = await Folder.CheckExistsAsync(fi.FileName);

            if (fileExists == ExistenceCheckResult.NotFound)
            {
                return bytes;
            }

            var file = await Folder.GetFileAsync(fi.FileName);

            using (var stream = await file.OpenAsync(FileAccess.Read))
            {
                bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
            }

            return bytes;
        }

        public async Task WriteAsync(CacheItemInfo info, byte[] data)
        {
            var fileInfo = new HashedFileItem(info);
            await EnsureFolderExists(fileInfo.FolderName);
            var file = await Folder.CreateFileAsync(fileInfo.FileName, CreationCollisionOption.ReplaceExisting);

            using (var stream = await file.OpenAsync(FileAccess.ReadAndWrite))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            _cache[info.UniqueName] = info;
        }

        private async Task<IFolder> EnsureFolderExists(string folderPath)
        {
            var folderNames = new List<string> { CacheFolderName, folderPath };
            var currentFolder = Folder;
            foreach (var folderName in folderNames)
            {
                currentFolder = await currentFolder.CreateFolderAsync(folderName, CreationCollisionOption.OpenIfExists);
            }
            return currentFolder;
        }

        public async Task CleanupAsync(DateTime maximumExpirationTime)
        {
            var allItems = await GetItemsAsync();
            // get the list of items.
            //
            var itemsToCleanup = from item in allItems
                                 where item.ExpirationTime <= maximumExpirationTime
                                 select item;

            // we snap the enumerable to an array to guard against any provider
            // implementations that might have returned an enumerator that would be affected
            // by the delete operation.
            //
            var deletionTasks = new List<Task>();
            foreach (var item in itemsToCleanup.ToArray())
            {
                deletionTasks.Add(DeleteAsync(item));
            }
            await Task.WhenAll(deletionTasks);
        }

        private async Task<IEnumerable<string>> GetFileNamesRecursive(string root)
        {
            var fileNames = new List<string>();
            if (await Folder.CheckExistsAsync(root) != ExistenceCheckResult.FolderExists)
            {
                return fileNames;
            }

            var rootFolder = await Folder.GetFolderAsync(root);
            var filesInFolder = await rootFolder.GetFilesAsync();

            fileNames.AddRange(filesInFolder.Select(file => file.Path));

            var foldersInRootFolder = await rootFolder.GetFoldersAsync();

            foreach (var folder in foldersInRootFolder)
            {
                fileNames.AddRange(await GetFileNamesRecursive(PortablePath.Combine(root, folder.Name)));
            }
            return fileNames;
        }
    }
}