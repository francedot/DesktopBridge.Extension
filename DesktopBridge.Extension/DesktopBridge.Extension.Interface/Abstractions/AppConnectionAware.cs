using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using DesktopBridge.Extension.Interface.Services;

namespace DesktopBridge.Extension.Interface.Abstractions
{
    public abstract class AppConnectionAware : Application
    {
        private BackgroundTaskDeferral _appServiceDeferral;
        public static AppServiceConnection Connection;

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);
            await DesktopBridgeExtension.Instance.InitializeAsync();
        }

        /// <summary>
        /// Initializes the app service on the host process 
        /// </summary>
        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);
            if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails)
            {
                _appServiceDeferral = args.TaskInstance.GetDeferral();
                args.TaskInstance.Canceled += OnTaskCanceled;

                AppServiceTriggerDetails details = args.TaskInstance.TriggerDetails as AppServiceTriggerDetails;

                // Handle AppService absence
                Connection = details?.AppServiceConnection;
                DesktopBridgeExtension.Instance.InflateConnection(Connection);
            }
        }

        /// <summary>
        /// Associate the cancellation handler with the background task 
        /// </summary>
        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            // Complete the service deferral.
            _appServiceDeferral?.Complete();
        }
    }
}