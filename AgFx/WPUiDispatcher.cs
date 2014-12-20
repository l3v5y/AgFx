using System;
using System.Windows;
using System.Windows.Threading;

namespace AgFx
{
    /// <summary>
    /// A Ui Dispatcher example for Windows Phone
    /// </summary>
    public class WPUiDispatcher : IUiDispatcher
    {
        private readonly Lazy<Dispatcher> _uiDispatcher;

        /// <summary>
        /// Default constructor
        /// </summary>
        public WPUiDispatcher()
        {
            _uiDispatcher = new Lazy<Dispatcher>(() => Deployment.Current.Dispatcher);
        }

        public void Dispatch(Action action)
        {
            _uiDispatcher.Value.BeginInvoke(action);
        }

        public bool IsOnUiThread()
        {
            return _uiDispatcher.Value.CheckAccess();
        }
    }
}