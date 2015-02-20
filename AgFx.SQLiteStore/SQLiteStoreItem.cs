using SQLite.Net.Attributes;

namespace AgFx
{
    internal class SQLiteStoreItem
    {
        [PrimaryKey]
        public string UniqueName { get; set; }

        public byte[] Payload { get; set; }
    }
}