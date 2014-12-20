// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;

namespace AgFx.IsoStore
{
    /// <summary>
    ///     Default Isolated Storage StoreProvider
    /// </summary>
    public class HashedIsoStoreProvider : IStoreProvider
    {
        private const int DeleteRetryCount = 3;
        private readonly string _cacheDirectoryPrefix;
        private const int WriteRetries = 3;
        // TODO: Do we need to cache every single loaded object in memory here as well?
        private readonly Dictionary<string, CacheItemInfo> _cache;
        private readonly IsolatedStorageFile _isoStore;
        private readonly object _lockObject;

        /// <summary>
        ///     Store files in Isolated Storage, using the objects HashCode
        /// </summary>
        public HashedIsoStoreProvider(string cacheDirectoryName)
        {
            _cacheDirectoryPrefix = cacheDirectoryName + "\\";
            _cache = new Dictionary<string, CacheItemInfo>();
            _lockObject = new object();
            _isoStore = IsolatedStorageFile.GetUserStoreForApplication();
        }

        /// <summary>
        ///     Return all the items in the store.
        /// </summary>
        /// <returns>An enumerable of all the items in the store.</returns>
        public IEnumerable<CacheItemInfo> GetItems()
        {
            var files = GetFilePathsRecursive(_cacheDirectoryPrefix);
            var items = from f in files
                        select new FileItem(f, _cacheDirectoryPrefix).Item;

            return items;
        }

        /// <summary>
        ///     Delete all of the items with the given unique key.
        /// </summary>
        /// <param name="uniqueName">The unique key being deleted.</param>
        public void Delete(string uniqueName)
        {
            lock(_cache)
            {
                if(_cache.ContainsKey(uniqueName))
                {
                    _cache.Remove(uniqueName);
                }
            }

            // find the directory.
            //
            var dir = FileItem.DirectoryHash(uniqueName, _cacheDirectoryPrefix);

            if(_isoStore.DirectoryExists(dir))
            {
                lock(_lockObject)
                {
                    var files = _isoStore.GetFileNames(dir + "\\*");
                    foreach(var f in files)
                    {
                        var path = Path.Combine(dir, f);
                        DeleteFileHelper(_isoStore, path);
                    }
                }
            }
        }


        /// <summary>
        ///     Gets the items with the specified unique name.
        /// </summary>
        /// <param name="uniqueName">a unique key.</param>
        /// <returns>A CacheItemInfo object</returns>
        public CacheItemInfo GetItem(string uniqueName)
        {
            var items = GetItemsAndCleanup(uniqueName);

            return items.FirstOrDefault();
        }

        /// <summary>
        ///     Clears the store of all items.
        /// </summary>
        public void Delete()
        {
            var items = GetItems().ToArray();
            foreach(var item in items)
            {
                Delete(item);
            }
        }

        /// <summary>
        ///     Delete's the given item from the store.
        /// </summary>
        /// <param name="item">The item to delete</param>
        public void Delete(CacheItemInfo item)
        {
            lock(_cache)
            {
                CacheItemInfo cachedItem;
                if(_cache.TryGetValue(item.UniqueName, out cachedItem) && Equals(item, cachedItem))
                {
                    _cache.Remove(item.UniqueName);
                }
            }

            var fi = new FileItem(item, _cacheDirectoryPrefix);

            var fileName = fi.FileName;

            lock(_lockObject)
            {
                DeleteFileHelper(_isoStore, fileName);
            }
        }

        /// <summary>
        ///     Reads data from IsolatedStorage for a given CacheItemInfo
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public byte[] Read(CacheItemInfo item)
        {
            var fi = new FileItem(item, _cacheDirectoryPrefix);
            byte[] bytes;

            lock(_lockObject)
            {
                if(!_isoStore.FileExists(fi.FileName))
                {
                    return null;
                }

                using(Stream stream = _isoStore.OpenFile(fi.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, (int)stream.Length);
                }
            }

            return bytes;
        }

        /// <summary>
        ///     Writes data for a given CacheItemInfo
        /// </summary>
        /// <param name="info"></param>
        /// <param name="data"></param>
        public void Write(CacheItemInfo info, byte[] data)
        {
            var fi = new FileItem(info, _cacheDirectoryPrefix);

            lock(_lockObject)
            {
                for(var r = 0; r < WriteRetries; r++)
                {
                    try
                    {
                        EnsurePath(_isoStore, fi.FileName);
                        using(
                            Stream stream = _isoStore.OpenFile(fi.FileName, FileMode.Create, FileAccess.Write,
                                FileShare.None))
                        {
                            stream.Write(data, 0, data.Length);
                            stream.Flush();
                        }
                        _cache[info.UniqueName] = info;
                        break;
                    }
                    catch(IsolatedStorageException)
                    {
                        Debug.WriteLine("Exception writing file: Name={0}, Length={1}", fi.FileName, data.Length);
                        // These IsolatedStorageExceptions seem to happen at random,
                        // haven't yet found a repro.  So for the retry,
                        // if we failed, sleep for a bit and then try again.
                        //
                        Thread.Sleep(50);
                    }
                }
            }
        }

        private IEnumerable<CacheItemInfo> GetItemsAndCleanup(string uniqueName)
        {
            CacheItemInfo item;

            lock(_lockObject)
            {
                if(_cache.TryGetValue(uniqueName, out item))
                {
                    return new[] {item};
                }
            }

            // find the directory.
            //
            var dir = FileItem.DirectoryHash(uniqueName, _cacheDirectoryPrefix);

            if(!_isoStore.DirectoryExists(dir))
            {
                return new CacheItemInfo[0];
            }
            lock(_lockObject)
            {
                string[] files;
                try
                {
                    files = _isoStore.GetFileNames(dir + "\\*");
                }
                catch(IsolatedStorageException)
                {
                    // intermittent IsoStore exceptions on shutdown.
                    files = new string[0];
                }

                var items = files.Select(x => FileItem.FromFileName(x, _cacheDirectoryPrefix)).Where(cii => cii != null).ToList();

                var orderedItems = from i in items
                    where i.UniqueName == uniqueName
                    orderby i.ExpirationTime descending
                    select i;

                foreach(var i in orderedItems)
                {
                    if(item == null)
                    {
                        item = i;
                        continue;
                    }
                    Delete(i);
                }

                if(item != null)
                {
                    _cache[uniqueName] = item;
                    return new[] {item};
                }
            }
            return new CacheItemInfo[0];
        }

        private IEnumerable<string> GetFilePathsRecursive(string root)
        {
            var search = Path.Combine(root, "*");

            var files = new List<string>();

            try
            {
                files.AddRange(_isoStore.GetFileNames(search));

                foreach(var d in _isoStore.GetDirectoryNames(search))
                {
                    files.AddRange(GetFilePathsRecursive(Path.Combine(root, d)));
                }
            }
            catch(InvalidOperationException)
            {
            }
            catch(IsolatedStorageException)
            {
            }

            return files;
        }

        private static void DeleteFileHelper(IsolatedStorageFile isoStore, string path)
        {
            for(var i = 0; i < DeleteRetryCount; i++)
            {
                try
                {
                    if(isoStore.FileExists(path))
                    {
                        isoStore.DeleteFile(path);
                    }
                    else
                    {
                        return;
                    }
                }
                catch(IsolatedStorageException)
                {
                    // random iso-store failures..
                    // TODO: Investigate?
                    Thread.Sleep(50);
                }
            }
        }

        private void EnsurePath(IsolatedStorageFile store, string filename)
        {
            for(var path = Path.GetDirectoryName(filename);
                !String.IsNullOrEmpty(path);
                path = Path.GetDirectoryName(path))
            {
                if(!store.DirectoryExists(path))
                {
                    store.CreateDirectory(path);
                }
            }
        }
    }
}