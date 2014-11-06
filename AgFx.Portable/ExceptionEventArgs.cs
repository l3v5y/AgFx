// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.

using System;

namespace AgFx
{
    internal class ExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
    }
}
