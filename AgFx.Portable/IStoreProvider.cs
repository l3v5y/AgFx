// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgFx
{
    /// <summary>
    /// Interface for the AgFx store provider.
    /// </summary>
    public interface IStoreProvider
    {
        /// <summary>
        /// Clears the store of all items.
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Gets the items with the specified unique name.
        /// </summary>
        /// <param name="uniqueName">a unique key.</param>
        /// <returns>A set of CacheItemInfo objects</returns>
        Task<CacheItemInfo> GetItemAsync(string uniqueName);

        /// <summary>
        /// Return all the items in the store.  Avoid this, it's usually expensive.
        /// </summary>
        /// <returns>An enumerable of all the items in the store.</returns>
        Task<IEnumerable<CacheItemInfo>> GetItemsAsync();

        /// <summary>
        /// Delete's the given item from the store.
        /// </summary>
        /// <param name="item">The item to delete</param>
        Task DeleteAsync(CacheItemInfo item);
        
        /// <summary>
        /// Delete all of the items with the given unique key.
        /// </summary>
        /// <param name="uniqueName">The unique key being deleted.</param>
        Task DeleteAllAsync(string uniqueName);

        /// <summary>
        /// Read the data for the given item.
        /// </summary>
        /// <param name="item">The info describing the item to read</param>
        /// <returns>The data in the store for the specified item.</returns>
        Task<byte[]> ReadAsync(CacheItemInfo item);

        /// <summary>
        /// Write the item's data to the store.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="data"></param>
        Task WriteAsync(CacheItemInfo info, byte[] data);

        /// <summary>
        /// Deletes all items that expire before the given time
        /// </summary>
        /// <param name="maximumExpirationTime"></param>
        /// <returns></returns>
        Task CleanupAsync(DateTime maximumExpirationTime);       
    }
}
