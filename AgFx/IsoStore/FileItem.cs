using System;
using System.Diagnostics;
using System.IO;

namespace AgFx.IsoStore
{
    class FileItem
    {
        private const char FileNameSeparator = '»';
        private readonly string _cacheDirectoryPrefix;
        private string _fileName;
        private CacheItemInfo _item;

        public FileItem(string fileName, string cacheDirectoryPrefix)
        {
            _fileName = fileName;
            _cacheDirectoryPrefix = cacheDirectoryPrefix;
        }

        public FileItem(CacheItemInfo item, string cacheDirectoryPrefix)
        {
            Item = item;
            _cacheDirectoryPrefix = cacheDirectoryPrefix;
        }

        public CacheItemInfo Item
        {
            get
            {
                if (_item == null && _fileName != null)
                {
                    _item = FromFileName(_fileName, _cacheDirectoryPrefix);
                }
                Debug.Assert(_item != null, "No CacheItemInfo!");
                return _item;
            }
            private set { _item = value; }
        }

        public string FileName
        {
            get
            {
                if (_fileName == null)
                {
                    _fileName = ToFileName(Item, _cacheDirectoryPrefix);
                }
                return _fileName;
            }
        }

        public override bool Equals(object obj)
        {
            var other = (FileItem)obj;

            if (_fileName != null)
            {
                return other._fileName == _fileName;
            }
            if (_item != null)
            {
                return Equals(_item, other._item);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return FileName.GetHashCode();
        }

#if DEBUG

        public override string ToString()
        {
            return FileName;
        }
#endif
        public static CacheItemInfo FromFileName(string fileName, string cacheDirectoryPrefix)
        {
            if (!fileName.StartsWith(cacheDirectoryPrefix))
            {
                fileName = Path.GetFileName(fileName);

                var parts = fileName
                    .Split(FileNameSeparator);

                if (parts.Length == 4)
                {
                    var uniqueKey = DecodePathName(parts[0]);

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

        public static string ToFileName(CacheItemInfo item, string cacheDirectoryPrefix)
        {
            var name = EncodePathName(item.UniqueName);
            name = String.Format("{1}{0}{2}{0}{3}{0}{4}", FileNameSeparator, name, item.IsOptimized,
                item.ExpirationTime.Ticks, item.UpdatedTime.Ticks);
            name = Path.Combine(DirectoryHash(item.UniqueName, cacheDirectoryPrefix), name);
            return name;
        }

        public static string DirectoryHash(string uniqueName, string cacheDirectoryPrefix)
        {
            return Path.Combine(cacheDirectoryPrefix, uniqueName.GetHashCode().ToString());
        }

        private static string DecodePathName(string encodedPath)
        {
            return Uri.UnescapeDataString(encodedPath);
        }

        private static string EncodePathName(string path)
        {
            return Uri.EscapeDataString(path);
        }
    }
}
