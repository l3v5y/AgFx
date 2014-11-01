using System;

namespace AgFx
{
    public interface IDispatcher
    {
        bool IsOnUiThread { get; }
        void BeginInvoke(Action action);
    }
}
