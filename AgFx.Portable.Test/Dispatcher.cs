using System;
using System.Threading.Tasks;
using System.Windows;

namespace AgFx.Test
{
    public class Dispatcher : IDispatcher
    {
        public bool IsOnUiThread
        {
            get { return true; }
        }

        public void BeginInvoke(Action action)
        {
            Task.Run(action);
        }
    }
}