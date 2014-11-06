// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;

namespace AgFx
{
    internal class ValueAvailableEventArgs : EventArgs
    {
        public object Value { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}
