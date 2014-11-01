using System;

namespace AgFx
{
    public class DataManagerUnhandledExceptionEventArgs : EventArgs
    {
        public DataManagerUnhandledExceptionEventArgs(Exception ex, bool handled)
        {
            Exception = ex;
            Handled = handled;
        }

        public Exception Exception { get; private set; }

        public bool Handled { get; set; }
    }
}