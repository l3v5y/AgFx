using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgFx
{
    internal enum DataLoadState
    {
        None,
        Loading,
        Loaded,
        Processing,
        ValueAvailable,
        Failed
    }
}
