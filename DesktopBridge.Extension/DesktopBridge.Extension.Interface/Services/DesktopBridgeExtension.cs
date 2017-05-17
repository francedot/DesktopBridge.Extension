using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using DesktopBridge.Extension.Shared.Exceptions;
using DesktopBridge.Extension.Shared.Models;
using Newtonsoft.Json;

namespace DesktopBridge.Extension.Interface.Services
{
    public class DesktopBridgeExtension
    {
        internal delegate void ProcedureCompletedEventHandler<T>(object sender, ProcedureCompletedEventArgs<T> e);

        private static DesktopBridgeExtension _instance;
        private AppServiceConnection _middlewareConnection;
        private Timer _procedureTimeOutTimer;

        private DesktopBridgeExtension()
        {
        }

        public static DesktopBridgeExtension Instance => _instance ?? (_instance = new DesktopBridgeExtension());

        public List<Parameter> Parameters { get; set; }
        public List<string> Usings { get; set; }
        public List<string> References { get; set; }
        public Timer ProcedureTimeOutTimer => _procedureTimeOutTimer;

        public DesktopBridgeExtension WithParameter<TParam>(string paramName, TParam paramValue)
        {
            if (Parameters == null)
            {
                Parameters = new List<Parameter>();
            }

            Parameters.Add(
                new Parameter
                {
                    Name = paramName,
                    TypeName = typeof(TParam).FullName,
                    Value = JsonConvert.SerializeObject(paramValue)
                });

            return this;
        }

        public DesktopBridgeExtension WithUsing(string @using)
        {
            if (Usings == null)
            {
                Usings = new List<string>();
            }

            Usings.Add(@using);

            return this;
        }

        public DesktopBridgeExtension WithUsing(IEnumerable<string> usings)
        {
            if (Usings == null)
            {
                Usings = new List<string>();
            }

            Usings.AddRange(usings);

            return this;
        }

        public DesktopBridgeExtension WithReference(string referencePath)
        {
            if (References == null)
            {
                References = new List<string>();
            }

            References.Add(referencePath);

            return this;
        }

        public DesktopBridgeExtension WithReference(IEnumerable<string> referencePaths)
        {
            if (References == null)
            {
                References = new List<string>();
            }

            References.AddRange(referencePaths);

            return this;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Run the Proxy App
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                throw;
            }
        }

        internal void InflateConnection(AppServiceConnection appServiceConnection)
        {
            _middlewareConnection = appServiceConnection;
        }

        public async Task ExecuteMainProgramAsync(string code)
        {
            if (IsUsingWith())
            {
                throw new NotSupportedException("Use of With not supported on ExecuteMainProgram");
            }

            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("\"code\" is null or Empty");
            }

            var program = new ProgramRequest
            {
                Code = code,
                ProgramKind = ProgramKind.MainProgram,
            };

            var programResult = await ExecuteInternal(program);

            CheckProgramResult(programResult);
        }

        public async Task ExecuteMainProgramFromFileAsync(string path)
        {
            if (IsUsingWith())
            {
                throw new NotSupportedException("Use of With not supported on ExecuteMainProgram");
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("\"path\" is null or Empty");
            }

            var codeFile = await Package.Current.InstalledLocation.GetFileAsync(path);
            var code = await Windows.Storage.FileIO.ReadTextAsync(codeFile);

            await ExecuteMainProgramAsync(code);
        }

        public async Task ExecuteScriptAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("\"code\" is null or Empty");
            }

            var program = new ProgramRequest
            {
                Code = code,
                ProgramKind = ProgramKind.Script,
                Parameters = Parameters,
                References = References,
                Usings = Usings
            };

            var programResult = await ExecuteInternal(program);

            CheckProgramResult(programResult);
        }

        public async Task ExecuteScriptFromFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("\"path\" is null or Empty");
            }

            var codeFile = await Package.Current.InstalledLocation.GetFileAsync(path);
            var code = await Windows.Storage.FileIO.ReadTextAsync(codeFile);

            await ExecuteScriptAsync(code);
        }

        public async Task<TResult> ExecuteScriptAsync<TResult>(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("\"code\" is null or Empty");
            }

            var program = new ProgramRequest
            {
                Code = code,
                ReturnTypeName = typeof(TResult).FullName,
                ProgramKind = ProgramKind.Script,
                Parameters = Parameters,
                References = References,
                Usings = Usings
            };

            var programResult = await ExecuteInternal(program);

            CheckProgramResult(programResult);

            var result = (TResult) JsonConvert.DeserializeObject(programResult.Result, typeof(TResult));

            return result;
        }

        public async Task<TResult> ExecuteScriptFromFileAsync<TResult>(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("\"path\" is null or Empty");
            }

            var codeFile = await Package.Current.InstalledLocation.GetFileAsync(path);
            var code = await Windows.Storage.FileIO.ReadTextAsync(codeFile);

            var result = await ExecuteScriptAsync<TResult>(code);

            return result;
        }

        private bool IsUsingWith()
        {
            return Parameters != null || Usings != null || References != null;
        }

        private void CheckProgramResult(ProgramResult programResult)
        {
            if (!programResult.CompilationResult.Success)
            {
                throw new ProgramCompilationException("Compilation Error", programResult.CompilationResult.Diagnostics);
            }

            if (!programResult.Success)
            {
                throw programResult.Exception;
            }
        }

        private async Task<ProgramResult> ExecuteInternal(ProgramRequest program,
            CancellationToken token = default(CancellationToken))
        {
            // Clear Objects
            Parameters = default(List<Parameter>);
            Usings = default(List<string>);
            References = default(List<string>);

            var programSerialized = JsonConvert.SerializeObject(program);

            var vs = new ValueSet
            {
                {"Win32Request", programSerialized}
            };

            // Create the task to be returned
            var tcs = new TaskCompletionSource<ProgramResult>();
            var eventWrapper = new GenericEventWrapper<ProgramResult>();

            _procedureTimeOutTimer = null;
            CancellationTokenSource cts = null;
            if (token == default(CancellationToken))
            {
                cts = new CancellationTokenSource();
                token = cts.Token;
            }

            TypedEventHandler<AppServiceConnection, AppServiceRequestReceivedEventArgs> onRequestReceived;
            onRequestReceived = (sender, args) =>
            {
                eventWrapper.OnProcedureCompleted(
                    new ProcedureCompletedEventArgs<ProgramResult>(
                        JsonConvert.DeserializeObject<ProgramResult>((string) args.Request.Message["Win32Response"]),
                        null, false, tcs));
            };

            // Setup the callback event handler
            ProcedureCompletedEventHandler<ProgramResult> handler = null;
            handler = (sender, e) =>
            {
                HandleCompletion(tcs, e, args => args.Result, handler,
                    (middleware, completion) =>
                    {
                        eventWrapper.ProcedureCompleted -= completion;
                        _middlewareConnection.RequestReceived -= onRequestReceived;
                    });
            };

            eventWrapper.ProcedureCompleted += handler;
            _middlewareConnection.RequestReceived += onRequestReceived;

            cts?.CancelAfter(TimeSpan.FromSeconds(30));

            _procedureTimeOutTimer = new Timer(state =>
            {
                if (!token.IsCancellationRequested)
                {
                    return;
                }
                _procedureTimeOutTimer.Dispose();

                HandleCompletion(tcs,
                    new ProcedureCompletedEventArgs<ProgramResult>(new ProgramResult(), null, true, tcs),
                    args => args.Result,
                    handler,
                    (middleware, completion) =>
                    {
                        eventWrapper.ProcedureCompleted -= completion;
                        _middlewareConnection.RequestReceived -= onRequestReceived;
                    });
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

            // Start the async operation.
            await _middlewareConnection.SendMessageAsync(vs);

            // Return the task that represents the async operation
            return await tcs.Task.ContinueWith((t) =>
            {
                ProcedureTimeOutTimer?.Dispose();
                return t.Result;
            }, token);
        }

        private void HandleCompletion<TAsyncCompletedEventArgs, TCompletionDelegate, T>(TaskCompletionSource<T> tcs,
            TAsyncCompletedEventArgs e, Func<TAsyncCompletedEventArgs, T> getResult, TCompletionDelegate handler,
            Action<AppServiceConnection, TCompletionDelegate> unregisterHandler)
            where TAsyncCompletedEventArgs : AsyncCompletedEventArgs
        {
            if (e.UserState != tcs)
            {
                return;
            }

            try
            {
                unregisterHandler(_middlewareConnection, handler);
            }
            finally
            {
                if (e.Error != null)
                    tcs.TrySetException(e.Error);
                else if
                    (e.Cancelled) tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(getResult(e));
            }
        }

        internal class ProcedureCompletedEventArgs<TResult> : AsyncCompletedEventArgs
        {
            private readonly TResult _result;

            internal ProcedureCompletedEventArgs(TResult result, Exception exception, bool cancelled,
                object userToken) : base(exception, cancelled, userToken)
            {
                _result = result;
            }

            public TResult Result
            {
                get
                {
                    RaiseExceptionIfNecessary();
                    return _result;
                }
            }
        }

        internal class GenericEventWrapper<T>
        {
            internal event ProcedureCompletedEventHandler<T> ProcedureCompleted;

            internal void OnProcedureCompleted(ProcedureCompletedEventArgs<T> e)
            {
                ProcedureCompleted?.Invoke(this, e);
            }
        }
    }
}