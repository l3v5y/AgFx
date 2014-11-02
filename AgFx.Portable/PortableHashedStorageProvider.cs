using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCLStorage;
using System.Diagnostics;

namespace AgFx
{
    public class PortableHashedStorageProvider : StoreProviderBase
    {
        private const char FileNameSeparator = '»';

        private const string CacheDirectoryName = "«c";

        internal const string CacheDirectoryPrefix = CacheDirectoryName + "\\";

        internal const string CacheDirectorySearchPrefix = CacheDirectoryPrefix + "*";

        Dictionary<string, CacheItemInfo> _cache = new Dictionary<string, CacheItemInfo>();

        public override bool IsBuffered
        {
            get { return false; }
        }

        private IFolder currentFolder
        {
            get
            {
                return FileSystem.Current.LocalStorage;
            }
        }

        private async Task<IEnumerable<string>> GetFileNamesRecursive(string root)
        {
            var fileNames = new List<string>();
            if(await currentFolder.CheckExistsAsync(root) != ExistenceCheckResult.FolderExists)
            {
                return fileNames;
            }

            var rootFolder = await currentFolder.GetFolderAsync(root);
            var filesInFolder = await rootFolder.GetFilesAsync();


            fileNames.AddRange(filesInFolder.Select(file => file.Path));

            var foldersInRootFolder = await rootFolder.GetFoldersAsync();

            foreach (var folder in foldersInRootFolder)
            {
                fileNames.AddRange(await GetFileNamesRecursive(PortablePath.Combine(root,folder.Name)));
            }
            return fileNames;
        }


        public override IEnumerable<CacheItemInfo> GetItems()
        {
            var items = new List<CacheItemInfo>();
            Task.Run(async () =>
            {
                var allFileNames =await GetFileNamesRecursive(CacheDirectoryPrefix);
                items = allFileNames.Select(f => new FileItem(f).Item).ToList();
            }).Wait();
            return items;
        }

        public override void Flush(bool synchronous)
        {
        }

        public override void Delete(CacheItemInfo item)
        {
            CacheItemInfo cachedItem;

            lock (_cache)
            {
                if (_cache.TryGetValue(item.UniqueName, out cachedItem) && Object.Equals(item, cachedItem))
                {
                    _cache.Remove(item.UniqueName);
                }
            }

            var fi = new FileItem(item);

            var fileName = fi.FileName;

            PriorityQueue.AddStorageWorkItem(async () =>
            {
                var file = await currentFolder.GetFileAsync(fileName);
                await file.DeleteAsync();
            });
        }

        public override byte[] Read(CacheItemInfo item)
        {
            var fi = new FileItem(item);
            byte[] bytes = null;
            Task.Run(async () =>
            {
                var fileExists = await currentFolder.CheckExistsAsync(fi.FileName);

                if (fileExists == ExistenceCheckResult.NotFound)
                {
                    return;
                }

                var file = await currentFolder.GetFileAsync(fi.FileName);

                using (var stream = await file.OpenAsync(FileAccess.Read))
                {
                    bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, (int)stream.Length);
                }

            }).Wait();
            return bytes;
        }

        public override async Task WriteAsync(CacheItemInfo info, byte[] data)
        {
            var fileInfo = new FileItem(info);

            var file = await currentFolder.CreateFileAsync(fileInfo.FileName, CreationCollisionOption.ReplaceExisting);

            using (var stream = await file.OpenAsync(FileAccess.ReadAndWrite))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            _cache[info.UniqueName] = info;
        }

        public override void Write(CacheItemInfo info, byte[] data)
        {
            Task.Run(async () =>
            {
                await WriteAsync(info, data);
            }).Wait();
        }

        private class FileItem
        {
            private byte[] _data;
            private string _fileName;
            private string _dirName;
            private CacheItemInfo _item;

            public CacheItemInfo Item
            {
                get
                {
                    if (_item == null && _fileName != null)
                    {
                        _item = FromFileName(_fileName);
                    }
                    Debug.Assert(_item != null, "No CacheItemInfo!");
                    return _item;
                }
                private set
                {
                    _item = value;
                }
            }

            public DateTime WriteTime;

            public byte[] Data
            {
                get
                {
                    return _data;
                }
                set
                {
                    if (_data != value)
                    {
                        _data = value;
                        WriteTime = DateTime.Now;
                    }
                }
            }

            public string DirectoryName
            {
                get
                {
                    if (_dirName == null)
                    {
                        _dirName = Item.UniqueName.GetHashCode().ToString();
                    }
                    return _dirName;
                }
            }

            public string FileName
            {
                get
                {
                    if (_fileName == null)
                    {
                        _fileName = ToFileName(Item);
                    }
                    return _fileName;
                }
            }

            public int Length
            {
                get
                {
                    if (_data == null)
                    {
                        return 0;
                    }
                    return _data.Length;
                }
            }

            public FileItem(string fileName)
            {
                _fileName = fileName;
            }

            public FileItem(CacheItemInfo item)
            {
                Item = item;
            }

            public override bool Equals(object obj)
            {
                var other = (FileItem)obj;

                if (_fileName != null)
                {
                    return other._fileName == _fileName;
                }
                else if (_item != null)
                {
                    return Object.Equals(_item, other._item);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return FileName.GetHashCode();
            }
            
            public static string DirectoryHash(string uniqueName)
            {
                return PortablePath.Combine(CacheDirectoryPrefix, uniqueName.GetHashCode().ToString());
            }

            public static CacheItemInfo FromFileName(string fileName)
            {
                if (!fileName.StartsWith(CacheDirectoryPrefix))
                {
                    fileName = fileName.Split(PortablePath.DirectorySeparatorChar).Last();

                    string[] parts = fileName
                        .Split(FileNameSeparator);

                    if (parts.Length == 4)
                    {

                        string uniqueKey = DecodePathName(parts[0]);

                        var item = new CacheItemInfo(uniqueKey)
                        {
                            ExpirationTime = new DateTime(Int64.Parse(parts[2])),
                            UpdatedTime = new DateTime(Int64.Parse(parts[3])),
                            IsOptimized = Boolean.Parse(parts[1])
                        };

                        return item;
                    }
                }
                return null;
            }

            private static string DecodePathName(string encodedPath)
            {
                return Uri.UnescapeDataString(encodedPath);
            }

            private static string EncodePathName(string path)
            {

                return Uri.EscapeDataString(path);
            }

            private static string ToFileName(CacheItemInfo item)
            {
                string name = EncodePathName(item.UniqueName);
                name = String.Format("{1}{0}{2}{0}{3}{0}{4}", FileNameSeparator, name, item.IsOptimized, item.ExpirationTime.Ticks, item.UpdatedTime.Ticks);
                name = PortablePath.Combine(DirectoryHash(item.UniqueName), name);
                return name;
            }
        }
    }
}
