using System;
using System.Windows;
using DesktopBridge.Extension.Proxy.Core.Services;

namespace DesktopBridge.Extension.Proxy.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.Visibility = Visibility.Hidden;
            this.WindowState = WindowState.Minimized;
            this.ShowInTaskbar = false;
            InitializeComponent();
        }

        protected override async void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            await DesktopBridgeMiddleware.Instance.WaitRequest();
        }
    }
}
