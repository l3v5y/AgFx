// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.


using System.Collections.Generic;

namespace AgFx
{
    /// <summary>
    ///     Base class for the AgFx store provider.
    ///     Ideally you could redirect AgFx to use a database or non-IsoStore implemenation.
    ///     This is not currently a tested scenario.
    /// </summary>
    public interface IStoreProvider
    {
        /// <summary>
        ///     Clears the store of all items.
        /// </summary>
        void Delete();

        /// <summary>
        ///     Gets the items with the specified unique name.
        /// </summary>
        /// <param name="uniqueName">a unique key.</param>
        /// <returns>A CacheItemInfo object</returns>
        CacheItemInfo GetItem(string uniqueName);

        /// <summary>
        ///     Return all the items in the store.  Avoid this, it's usually expensive.
        /// </summary>
        /// <returns>An enumerable of all the items in the store.</returns>
        IEnumerable<CacheItemInfo> GetItems();

        /// <summary>
        ///     Delete all of the items with the given unique key.
        /// </summary>
        /// <param name="uniqueName">The unique key being deleted.</param>
        void Delete(string uniqueName);

        /// <summary>
        ///     Read the data for the given item.
        /// </summary>
        /// <param name="item">The info describing the item to read</param>
        /// <returns>The data in the store for the specified item.</returns>
        byte[] Read(CacheItemInfo item);

        /// <summary>
        ///     Write the item's data to the store.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="data"></param>
        void Write(CacheItemInfo info, byte[] data);
    }
}