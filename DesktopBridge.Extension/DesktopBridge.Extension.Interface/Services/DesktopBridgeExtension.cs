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
    /// <summary>
    /// Provides methods to execute Win32 Scripts and Main Programs from UWP
    /// </summary>
    public class DesktopBridgeExtension
    {
        internal delegate void ProcedureCompletedEventHandler<T>(object sender, ProcedureCompletedEventArgs<T> e);

        private static DesktopBridgeExtension _instance;
        private AppServiceConnection _middlewareConnection;
        private Timer _procedureTimeOutTimer;

        private DesktopBridgeExtension()
        {
        }

        /// <summary>
        /// DesktopBridgeExtension Singleton
        /// </summary>
        public static DesktopBridgeExtension Instance => _instance ?? (_instance = new DesktopBridgeExtension());

        public List<Parameter> Parameters { get; set; }
        public List<string> Usings { get; set; }
        public List<string> References { get; set; }
        public Timer ProcedureTimeOutTimer => _procedureTimeOutTimer;

        /// <summary>
        /// Adds a Parameter to be passed as a part of the Script
        /// </summary>
        /// <typeparam name="TParam">The Type of the Parameter</typeparam>
        /// <param name="paramName">Name of the Parameter</param>
        /// <param name="paramValue">Value of the Parameter</param>
        /// <returns></returns>
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

        /// <summary>
        /// Adds a Using directive to be passed as a part of the Script
        /// </summary>
        /// <param name="using">The using namespace (without the using keyword)</param>
        /// <returns></returns>
        public DesktopBridgeExtension WithUsing(string @using)
        {
            if (Usings == null)
            {
                Usings = new List<string>();
            }

            Usings.Add(@using);

            return this;
        }

        /// <summary>
        /// Adds a collection of Using directives to be passed as a part of the Script
        /// </summary>
        /// <param name="usings">A collection of using namespaces (without the using keyword)</param>
        /// <returns></returns>
        public DesktopBridgeExtension WithUsing(IEnumerable<string> usings)
        {
            if (Usings == null)
            {
                Usings = new List<string>();
            }

            Usings.AddRange(usings);

            return this;
        }

        /// <summary>
        /// Adds an assembly path to be referenced by the Script
        /// </summary>
        /// <param name="referencePath">Path to the assembly relative to the project folder</param>
        /// <returns></returns>
        public DesktopBridgeExtension WithReference(string referencePath)
        {
            if (References == null)
            {
                References = new List<string>();
            }

            References.Add(referencePath);

            return this;
        }

        /// <summary>
        /// Adds a collection of assembly paths to be referenced by the Script
        /// </summary>
        /// <param name="referencePaths">Paths to the assemblies relative to the installed location</param>
        /// <returns></returns>
        public DesktopBridgeExtension WithReference(IEnumerable<string> referencePaths)
        {
            if (References == null)
            {
                References = new List<string>();
            }

            References.AddRange(referencePaths);

            return this;
        }

        /// <summary>
        /// Starts the embedded full trust process
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Inflates the DesktopBridgeMiddleware App Service Connection
        /// </summary>
        /// <param name="appServiceConnection">The DesktopBridgeMiddleware App Service Connection</param>
        public void InflateConnection(AppServiceConnection appServiceConnection)
        {
            _middlewareConnection = appServiceConnection;
        }

        /// <summary>
        /// Executes the Main Program
        /// </summary>
        /// <param name="code">The code of the Main Program</param>
        /// <returns></returns>
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

        /// <summary>
        /// Executes the Main Program
        /// </summary>
        /// <param name="path">The path to a file containing the Main Program relative to the installed location</param>
        /// <returns></returns>
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

        /// <summary>
        /// Executes the Code Script
        /// </summary>
        /// <param name="code">The code of the Script</param>
        /// <returns></returns>
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

        /// <summary>
        /// Executes the Code Script
        /// </summary>
        /// <param name="path">The path to a file containing the code of the Script relative to the installed location</param>
        /// <returns></returns>
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

        /// <summary>
        /// Executes the Code Script and returns a result value
        /// </summary>
        /// <typeparam name="TResult">The Type of the expected Result</typeparam>
        /// <param name="code">The code of the Script</param>
        /// <returns>The evaluated result</returns>
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

        /// <summary>
        /// Executes the Code Script and returns a result value
        /// </summary>
        /// <typeparam name="TResult">The Type of the expected Result</typeparam>
        /// <param name="path">The path to a file containing the code of the Script relative to the installed location</param>
        /// <returns>The evaluated result</returns>
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