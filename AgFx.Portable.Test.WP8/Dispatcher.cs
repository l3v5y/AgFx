using System;
using System.Windows;

namespace AgFx.Test
{
    public class Dispatcher : IDispatcher
    {
        private static System.Windows.Threading.Dispatcher dispatcher
        {
            get { return Deployment.Current.Dispatcher; }
        }

        public bool IsOnUiThread
        {
            get { return dispatcher.CheckAccess(); }
        }

        public void BeginInvoke(Action action)
        {
            dispatcher.BeginInvoke(action);
        }
    }
}