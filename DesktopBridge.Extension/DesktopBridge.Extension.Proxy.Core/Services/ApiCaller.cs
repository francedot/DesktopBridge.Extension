using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using DesktopBridge.Extension.Shared.Exceptions;
using DesktopBridge.Extension.Shared.Extensions;
using DesktopBridge.Extension.Shared.Models;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using CompilationResult = DesktopBridge.Extension.Shared.Models.CompilationResult;
using Diagnostic = DesktopBridge.Extension.Shared.Models.Diagnostic;

namespace DesktopBridge.Extension.Proxy.Core.Services
{
    internal class ApiCaller
    {
        private static readonly List<Assembly> DefaultAssemblies;
        private static readonly List<MetadataReference> DefaultReferences;
        private static readonly List<string> DefaultUsings;
        private static readonly string BaseCode;

        static ApiCaller()
        {
            var frameworkPath = GetPathToDotNetFrameworkReferenceLatest();
            var facadePath = frameworkPath + @"Facades\";

            var executionPath = Package.Current.InstalledLocation.Path;
            //var executionPath = Directory.GetCurrentDirectory(); // WPF startup

            BaseCode = $"System.IO.Directory.SetCurrentDirectory(@\"{executionPath}\");" + Environment.NewLine;

            var assemblyPaths =
                (from runAssemblyPath
                in Directory.GetFiles(frameworkPath, "*.dll")
                where !runAssemblyPath.Contains("System.EnterpriseServices.Wrapper") &&
                      !runAssemblyPath.Contains("System.EnterpriseServices.Thunk")
                select runAssemblyPath).Union(
                from facAssemblyPath
                in Directory.GetFiles(facadePath, "*.dll")
                select facAssemblyPath).ToList();

            DefaultAssemblies =
            (from assemblyPath in assemblyPaths
                select Assembly.LoadFile(assemblyPath)).ToList();

            DefaultReferences =
            (from assemblyPath in assemblyPaths
                select MetadataReference.CreateFromFile(assemblyPath)).Cast<MetadataReference>().ToList();

            DefaultUsings = new List<string>
            {
                "System",
                "System.Text",
                "System.Linq",
                "System.Threading.Tasks",
                "System.Collections.Generic"
            };
        }

        internal async Task<ProgramResult> ExecuteAsync(ProgramRequest program,
            CancellationToken token = default(CancellationToken))
        {
            ProgramResult result = null;

            if (program.ProgramKind == ProgramKind.Script)
            {
                result = await ExecuteWin32Script(program, token);
            }
            else if (program.ProgramKind == ProgramKind.MainProgram)
            {
                result = ExecuteMainProgram(program, token);
            }

            return result;
        }

        internal ProgramResult ExecuteMainProgram(ProgramRequest program,
            CancellationToken token = default(CancellationToken))
        {
            var programPath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "MainProgram.exe");

            var st = CSharpSyntaxTree.ParseText(program.Code);
            var compilation =
                CSharpCompilation.Create(
                    assemblyName: "MainProgram.exe",
                    syntaxTrees: new[] {st},
                    references: DefaultReferences);

            var programStream = new FileStream(programPath, FileMode.OpenOrCreate);
            
            // Emit the Assembly in FS
            var emitResult = compilation.Emit(programStream);

            programStream.Dispose();

            var diagnostics = emitResult.Diagnostics;
            var resultDiagnostics = GetResultDiagnostics(diagnostics);

            var programResult = new ProgramResult
            {
                CompilationResult = new CompilationResult
                {
                    Diagnostics = resultDiagnostics,
                    Success = emitResult.Success
                }
            };

            if (!emitResult.Success)
            {
                return programResult;
            }

            var process = new Process();
            var info = new ProcessStartInfo
            {
                FileName = programPath
            };
            process.StartInfo = info;
            process.Start();
            Thread.Sleep(100); // Wait for the MainWindow to be loaded so that to get its Handle
            SetWindowText(process.MainWindowHandle, Package.Current.DisplayName);

            return programResult;
        }

        private async Task<ProgramResult> ExecuteWin32Script(ProgramRequest program,
            CancellationToken token = default(CancellationToken))
        {
            var programResult = new ProgramResult();

            Script<object> script;
            object res;
            object globals = null;

            IEnumerable<string> usings = DefaultUsings;
            if (program.Usings != null && program.Usings.Any())
            {
                usings = DefaultUsings.Union(program.Usings);
            }

            var assemblies = DefaultAssemblies;
            var references = DefaultReferences;
            if (program.References != null && program.References.Any())
            {
                foreach (var referencePath in program.References)
                {
                    assemblies.Add(Assembly.LoadFile(referencePath));
                    references.Add(MetadataReference.CreateFromFile(referencePath));
                }
            }

            var fileResolver = new SourceFileResolver(ImmutableArray<string>.Empty, AppContext.BaseDirectory + @"..\");

            var code = BaseCode + program.Code;

            if (program.Parameters != null && program.Parameters.Any())
            {
                Assembly loadedGlobals;
                var globalsType = SaveGlobalsType(Thread.GetDomain(), program.Parameters, assemblies,
                    out loadedGlobals);
                globals = CreateGlobalsInstance(globalsType, program.Parameters);

                script = CSharpScript.Create(code,
                    ScriptOptions.Default.WithSourceResolver(fileResolver).WithImports(usings)
                        .WithReferences(references).AddReferences(loadedGlobals),
                    globalsType);
            }
            else
            {
                script = CSharpScript.Create(code,
                    ScriptOptions.Default.WithSourceResolver(fileResolver).WithImports(usings)
                        .WithReferences(references));
            }
            var diagnostics = script.Compile(token);
            var resultDiagnostics = GetResultDiagnostics(diagnostics);

            var success = !resultDiagnostics.HasErrors();
            programResult.CompilationResult = new CompilationResult
            {
                Diagnostics = resultDiagnostics,
                Success = success
            };

            if (!success)
            {
                return programResult;
            }

            var runner = script.CreateDelegate();

            try
            {
                // Run on UI Thread to cover most of the scenarios
                res = await System.Windows.Application.Current.Dispatcher.Invoke(
                        async () => await runner.Invoke(globals, token));
            }
            catch (Exception e)
            {
                programResult.Exception = new ProgramExecutionException("There was a problem executing the script", e);
                return programResult;
            }

            if (!program.HasReturnType)
            {
                return programResult;
            }

            var returnType = GetTypeFromName(program.ReturnTypeName);
            var ret = Convert.ChangeType(res, returnType);
            var result = JsonConvert.SerializeObject(ret);
            programResult.ReturnTypeName = program.ReturnTypeName;
            programResult.Result = result;

            return programResult;
        }

        private List<Diagnostic> GetResultDiagnostics(ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        {
            var resultDiagnostics = new List<Diagnostic>();

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);

            resultDiagnostics.AddRange(errors.Select(e =>
                new Diagnostic
                {
                    Kind = DiagnosticKind.Error,
                    Description = e.GetMessage(),
                    Code = e.Id
                }));
            resultDiagnostics.AddRange(warnings.Select(e =>
                new Diagnostic
                {
                    Kind = DiagnosticKind.Warning,
                    Description = e.GetMessage(),
                    Code = e.Id
                }));

            return resultDiagnostics;
        }

        private object CreateGlobalsInstance(Type globalsType, IEnumerable<Parameter> parameters)
        {
            var globals = globalsType.GetConstructor(new Type[] { })?.Invoke(new object[] { });

            foreach (var programParameter in parameters)
            {
                var propName = programParameter.Name;
                var prop = globalsType.GetRuntimeProperties().FirstOrDefault(p => p.Name == propName);
                var parameterType = GetTypeFromName(programParameter.TypeName);
                var parameterValue = JsonConvert.DeserializeObject(programParameter.Value, parameterType);
                prop?.SetValue(globals, Convert.ChangeType(parameterValue, parameterType), null);
            }

            return globals;
        }

        private Type SaveGlobalsType(AppDomain appDomain, IEnumerable<Parameter> parameters,
            List<Assembly> lookupAssemblies, out Assembly loadedType)
        {
            var outputPath = ApplicationData.Current.TemporaryFolder.Path;
            var asmName = new AssemblyName {Name = "Globals"};
            var asmBuild = appDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave, outputPath);
            var modBuild = asmBuild.DefineDynamicModule("ModuleGlobals", "Globals.dll");
            var tb = modBuild.DefineType("Globals", TypeAttributes.Public);
            ConstructProperties(tb, parameters, lookupAssemblies);

            tb.CreateType();
            asmBuild.Save("Globals.dll");

            loadedType = Assembly.LoadFile(outputPath + @"\Globals.dll");
            var globalsType = loadedType.GetType("Globals");

            return globalsType;
        }

        private void ConstructProperties(TypeBuilder typeBuilder, IEnumerable<Parameter> parameters,
            List<Assembly> lookupAssemblies)
        {
            foreach (var parameter in parameters)
            {
                var fieldName = "_" + parameter.Name;

                var parameterType = GetTypeFromName(parameter.TypeName, lookupAssemblies);

                var fieldBuilder = typeBuilder.DefineField(fieldName, parameterType, FieldAttributes.Private);
                var propertyBuilder = typeBuilder.DefineProperty(parameter.Name, PropertyAttributes.HasDefault, parameterType, null);

                // The property set and property get methods require a special set of attributes
                const MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName |
                                                    MethodAttributes.HideBySig;

                // Define the "get" accessor method
                var getPropMethodBuilder =
                    typeBuilder.DefineMethod($"get_{parameter.Name}",
                        getSetAttr,
                        parameterType,
                        Type.EmptyTypes);

                var getPropIlGenerator = getPropMethodBuilder.GetILGenerator();

                getPropIlGenerator.Emit(OpCodes.Ldarg_0);
                getPropIlGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
                getPropIlGenerator.Emit(OpCodes.Ret);

                // Define the "set" accessor method
                var setPropMethodBuilder =
                    typeBuilder.DefineMethod($"set_{parameter.Name}",
                        getSetAttr,
                        null,
                        new[] {parameterType});

                var setPropIlGenerator = setPropMethodBuilder.GetILGenerator();

                setPropIlGenerator.Emit(OpCodes.Ldarg_0);
                setPropIlGenerator.Emit(OpCodes.Ldarg_1);
                setPropIlGenerator.Emit(OpCodes.Stfld, fieldBuilder);
                setPropIlGenerator.Emit(OpCodes.Ret);

                // Map getter and setter to PropertyBuilder
                propertyBuilder.SetGetMethod(getPropMethodBuilder);
                propertyBuilder.SetSetMethod(setPropMethodBuilder);
            }
        }

        private Type GetTypeFromName(string typeName, IEnumerable<Assembly> lookupAssemblies = null)
        {
            lookupAssemblies = lookupAssemblies ?? DefaultAssemblies;

            var result = lookupAssemblies.SelectMany(
                            assembly => assembly.GetTypes()).FirstOrDefault(type => type.FullName == typeName);

            if (result == null)
            {
                throw new NullReferenceException($"No type found for {typeName}");
            }

            return result;
        }

        private static string GetPathToDotNetFrameworkReferenceLatest()
        {
            return
                ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion
                    .Version47) ??
                ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion
                    .Version462) ??
                ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion
                    .Version461) ??
                throw new RuntimeException("Minimum version of .NET Framework required is 4.6.1", null);
        }

        [DllImport("user32.dll")]
        static extern int SetWindowText(IntPtr hWnd, string text);
    }
}