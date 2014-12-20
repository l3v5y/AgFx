using System;

namespace AgFx
{
    public static class PropertyChangedNotificationInterceptor
    {
        private static Lazy<IUiDispatcher> uiDispatcher = new Lazy<IUiDispatcher>(() => new WPUiDispatcher());

        public static void Intercept(object target, Action onPropertyChangedAction, string propertyName)
        {
            if(uiDispatcher.Value == null || uiDispatcher.Value.IsOnUiThread())
            {
                onPropertyChangedAction();
            }
            else
            {
                uiDispatcher.Value.Dispatch(onPropertyChangedAction);
            }
        }
    }
}
