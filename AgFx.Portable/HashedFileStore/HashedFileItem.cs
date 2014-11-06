// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using PCLStorage;
using System;
using System.Diagnostics;
using System.Linq;

namespace AgFx.HashedFileStore
{
    // TODO: Make value accessors cleaner
    // TODO: to/from file?
    internal class HashedFileItem
    {
        private byte[] _data;
        private string _fileName;
        private string folderName;
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

        public string FolderName
        {
            get
            {
                if (folderName == null)
                {
                    folderName = Item.UniqueName.GetHashCode().ToString();
                }
                return folderName;
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

        public HashedFileItem(string fileName)
        {
            _fileName = fileName;
        }

        public HashedFileItem(CacheItemInfo item)
        {
            Item = item;
        }

        public override bool Equals(object obj)
        {
            var other = (HashedFileItem)obj;

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

        public static string FolderHash(string uniqueName)
        {
            return PortablePath.Combine(HashedFileStoreProvider.CacheFolderName, uniqueName.GetHashCode().ToString());
        }

        public static CacheItemInfo FromFileName(string fileName)
        {
            if (!fileName.StartsWith(HashedFileStoreProvider.CacheFolderName))
            {
                fileName = fileName.Split(PortablePath.DirectorySeparatorChar).Last();

                string[] parts = fileName.Split(HashedFileStoreProvider.FileNameSeparator);

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

        // TODO: implement a better/faster encode/decode
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
            var encodedPathName = EncodePathName(item.UniqueName);
            var fileName = String.Format("{1}{0}{2}{0}{3}{0}{4}",
                HashedFileStoreProvider.FileNameSeparator,
                encodedPathName,
                item.IsOptimized,
                item.ExpirationTime.Ticks,
                item.UpdatedTime.Ticks);
            var directoryName = FolderHash(item.UniqueName);
            return PortablePath.Combine(directoryName, fileName);
        }
    }
}