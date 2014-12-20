// (c) Copyright Microsoft Corporation.
// This source is subject to the Apache License, Version 2.0
// Please see http://www.apache.org/licenses/LICENSE-2.0 for details.
// All other rights reserved.


using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace AgFx
{
    /// <summary>
    ///     Base class for INotifyPropertyChanged implementation. Gives a RaisePropertyChanged method
    ///     for notifying changes as well a facilities for thread switching and change notification cascading.
    /// </summary>
    [DataContract]
    public class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        /// <summary>
        ///     INotifyPropertyChanged.PropertyChanged
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     Raise a property change for the given property on the notification thread.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void RaisePropertyChanged(string propertyName)
        {
            Action<string> notify = s =>
            {
                if(PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            };
            PropertyChangedNotificationInterceptor.Intercept(this, () => notify(propertyName), propertyName);
        }
    }
}