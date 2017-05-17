using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using DesktopBridge.Extension.SampleApp.ViewModels;

namespace DesktopBridge.Extension.SampleApp
{
    public sealed partial class MainPage : Page
    {
        public MainPageViewModel ViewModel => this.DataContext as MainPageViewModel;

        public MainPage()
        {
            this.InitializeComponent();
            DataContext = new MainPageViewModel();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.OnNavigatedToAsync();
        }
    }
}
