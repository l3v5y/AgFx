using System;

namespace AgFx
{
    /// <summary>
    /// Dispatch operations onto the current UI thread
    /// </summary>
    public interface IUiDispatcher
    {
        /// <summary>
        /// Dispatches an action to the Ui thread
        /// </summary>
        /// <param name="action">Action to dispatch</param>
        void Dispatch(Action action);
        
        /// <summary>
        /// Checks if we're currently on the Ui Thread
        /// </summary>
        /// <returns>True if on Ui Thread</returns>
        bool IsOnUiThread();
    }
}