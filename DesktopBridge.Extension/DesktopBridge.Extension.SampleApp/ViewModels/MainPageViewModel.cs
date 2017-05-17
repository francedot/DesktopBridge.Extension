using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Popups;
using DesktopBridge.Extension.Interface.Services;
using DesktopBridge.Extension.SampleApp.Models;
using DesktopBridge.Extension.SampleApp.Services;
using DesktopBridge.Extension.SampleApp.Utils;

namespace DesktopBridge.Extension.SampleApp.ViewModels
{
    public class MainPageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly CodeSnippetProvider _codeSnippetProvider = new CodeSnippetProvider();
        private ObservableCollection<CodeSnippet> _codeSnippets;
        private CodeSnippet _selectedCodeSnippet;
        private string _result;

        public MainPageViewModel()
        {
            ExecuteCommand = new Command(async () =>
            {
                try
                {
                    // APIs Usage Example

                    #region Script1

                    await DesktopBridgeExtension.Instance.ExecuteScriptFromFileAsync(@"Snippets\Code1.cs");

                    #endregion

                    #region Script2

                    var res = await DesktopBridgeExtension.Instance.WithParameter<int>("a", 1).
                                                                    WithParameter<int>("b", 4).
                                                                    ExecuteScriptAsync<int>(SelectedCodeSnippet.Content);
                    Result = $"Result is: {res}";

                    #endregion

                    #region Script3

                    await DesktopBridgeExtension.Instance.WithUsing("System.Drawing").
                                                          WithUsing("System.Windows").
                                                          WithUsing("System.Windows.Forms").
                                                          WithUsing("System.Diagnostics").
                                                          WithUsing("System.Runtime.InteropServices").
                                                          ExecuteScriptAsync(SelectedCodeSnippet.Content);

                    #endregion

                    #region MainProgram

                    await DesktopBridgeExtension.Instance.ExecuteMainProgramAsync(SelectedCodeSnippet.Content);

                    #endregion
                }
                catch (Exception e)
                {
                    await new MessageDialog($"Problem Executing Code: {e.Message}").ShowAsync();
                }
            });
        }

        public ObservableCollection<CodeSnippet> CodeSnippets
        {
            get => _codeSnippets;
            set
            {
                _codeSnippets = value;
                OnPropertyChanged();
            }
        }

        public CodeSnippet SelectedCodeSnippet
        {
            get => _selectedCodeSnippet;
            set
            {
                _selectedCodeSnippet = value;
                OnPropertyChanged();
            }
        }

        public string Result
        {
            get => _result;
            set
            {
                _result = value;
                OnPropertyChanged();
            }
        }

        public ICommand ExecuteCommand { get; }

        public async Task OnNavigatedToAsync()
        {
            CodeSnippets = new ObservableCollection<CodeSnippet>(
                await _codeSnippetProvider.GetCodeSnippetsAsync());
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}