﻿using System;

namespace AgFx
{
    /// <summary>
    /// Provides extended functionality to cached items.
    /// </summary>
    public interface ICachedItem
    {
        /// <summary>
        /// The time when the given item should expire from the cache.
        /// </summary>
        DateTime? ExpirationTime { get; }
    }
}