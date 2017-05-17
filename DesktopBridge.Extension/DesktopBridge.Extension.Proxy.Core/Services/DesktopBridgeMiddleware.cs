using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using DesktopBridge.Extension.Shared.Models;
using DesktopBridge.Extension.Shared.Exceptions;
using Newtonsoft.Json;

namespace DesktopBridge.Extension.Proxy.Core.Services
{
    internal class DesktopBridgeMiddleware
    {
        private static DesktopBridgeMiddleware _instance;
        private readonly ApiCaller _apiCaller;
        private static AppServiceConnection _middlewareConnection;

        private DesktopBridgeMiddleware()
        {
            _apiCaller = new ApiCaller();
        }

        internal static DesktopBridgeMiddleware Instance => _instance ?? (_instance = new DesktopBridgeMiddleware());

        internal async Task WaitRequest()
        {
            try
            {
                _middlewareConnection = new AppServiceConnection
                {
                    AppServiceName = "DesktopBridgeMiddleware",
                    PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName
                };
                _middlewareConnection.RequestReceived += Connection_RequestReceivedAsync;

                AppServiceConnectionStatus status = await _middlewareConnection.OpenAsync();
                switch (status)
                {
                    case AppServiceConnectionStatus.Success:
                        Debug.WriteLine("Connection established - waiting for requests");
                        break;
                    case AppServiceConnectionStatus.AppNotInstalled:
                        Debug.WriteLine("The app AppServicesProvider is not installed.");
                        return;
                    case AppServiceConnectionStatus.AppUnavailable:
                        Debug.WriteLine("The app AppServicesProvider is not available.");
                        return;
                    case AppServiceConnectionStatus.AppServiceUnavailable:
                        Debug.WriteLine(
                            $"The app AppServicesProvider is installed but it does not provide the app service {_middlewareConnection.AppServiceName}.");
                        return;
                    case AppServiceConnectionStatus.Unknown:
                        Debug.WriteLine("An unkown error occurred while we were trying to open an AppServiceConnection.");
                        return;
                    case AppServiceConnectionStatus.RemoteSystemUnavailable:
                    case AppServiceConnectionStatus.RemoteSystemNotSupportedByApp:
                    case AppServiceConnectionStatus.NotAuthorized:
                        Debug.WriteLine("An error occurred while we were trying to open an AppServiceConnection.");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message, "Title");
                Debug.WriteLine(e.StackTrace, "Title");
                throw;
            }
        }

        private async void Connection_RequestReceivedAsync(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            try
            {
                var message = args.Request.Message;

                if (!message.ContainsKey("Win32Request"))
                {
                    throw new Exception("No Key Win32Api");
                }

                var programRequest = JsonConvert.DeserializeObject<ProgramRequest>((string)message["Win32Request"]);

                var requestThread = new Thread(async () =>
                {
                    ProgramResult result;
                    try
                    {
                        result = await _apiCaller.ExecuteAsync(programRequest);

                        var resultSerialized = JsonConvert.SerializeObject(result);
                        var vs = new ValueSet
                        {
                            { "Win32Response", resultSerialized }
                        };

                        // Send back to UWP
                        await _middlewareConnection.SendMessageAsync(vs);
                    }
                    catch (Exception e)
                    {
                        result = new ProgramResult
                        {
                            Exception = new RuntimeException("Issue with the Desktop Bridge Proxy App", e)
                        };

                        var resultSerialized = JsonConvert.SerializeObject(result);
                        var vs = new ValueSet
                        {
                            { "Win32Response", resultSerialized }
                        };

                        await _middlewareConnection.SendMessageAsync(vs);
                    }
                });
                requestThread.Start();
            }
            catch (Exception e)
            {
                ProgramResult result = new ProgramResult
                {
                    Exception = new RuntimeException("DesktopBridge Proxy Problem", e)
                };

                var resultSerialized = JsonConvert.SerializeObject(result);
                var vs = new ValueSet
                {
                    { "Win32Response", resultSerialized }
                };

                await _middlewareConnection.SendMessageAsync(vs);
            }
        }
    }
}