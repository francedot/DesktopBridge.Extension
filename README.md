## Desktop Bridge Extension Library for the UWP

The DesktopBridge.Extension Library allows for Win32 APIs to be called from the UWP (x86, x64) abstracting the standard IPC (Inter Process Comunication) approach in favour of a more testable API Interface.

Nuget package is in the works. If you want to try it out before its release just clone the repo.

### Setup

* Reference **DesktopBridge.Extension.Interface** and **Reference DesktopBridge.Extension.Shared** from the UWP project.
* Modify the project **Package.appxmanifest**:

  1. Add rescap and desktop namespaces: 
  ```xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"```
  ```xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"```
  2. Add ignorable namespaces: ```IgnorableNamespaces="uap mp rescap desktop"```
  3. Add the AppService Extension called DesktopBridgeMiddleware and register the embedded executable for running as a full trust process
```xml
<Extensions>
  <uap:Extension Category="windows.appService">
    <uap:AppService Name="DesktopBridgeMiddleware" />
  </uap:Extension>
  <desktop:Extension Category="windows.fullTrustProcess" Executable="desktop\DesktopBridge.Extension.Proxy.App.exe" />
</Extensions> 
```

See the [DesktopBridge.Extension.SampleApp](https://github.com/francedot/DesktopBridge.Extension/tree/master/DesktopBridge.Extension/DesktopBridge.Extension.SampleApp) for further reference.

### API Usage

1. Make the App class inherit the **AppConnectionAware** abstract class in order for the APIs to gain access to the DesktopBridgeMiddleware AppService.
2. In the App **OnLaunched**, call the base class implementation ```base.OnLaunched(e)```. This is responsible for initializing the AppService and launching the registered full trust process.

Alternatively, make sure to obtain an instance of the DesktopBridgeMiddleware AppService by overriding the App **OnBackgroundActivated** and pass the **AppServiceConnection** to the **InflateConnection()** method of the **DesktopBridgeExtension** class. Lastly, call **InitializeAsync()** to start the full trust process.

```csharp
/// <summary>
/// Starts the embedded full trust process
/// </summary>
/// <returns></returns>
public async Task InitializeAsync();

/// <summary>
/// Inflates the DesktopBridgeMiddleware App Service Connection
/// </summary>
/// <param name="appServiceConnection">The DesktopBridgeMiddleware App Service Connection</param>
public void InflateConnection(AppServiceConnection appServiceConnection);
```

The **DesktopBridgeExtension** exposes its implementation through its singleton: ```DesktopBridgeExtension.Instance```

### Executing Main Programs

```csharp

/// <summary>
/// Executes the Main Program
/// </summary>
/// <param name="code">The code of the Main Program</param>
/// <returns></returns>
public async Task ExecuteMainProgramAsync(string code);

/// <summary>
/// Executes the Main Program
/// </summary>
/// <param name="path">The path to a file containing the Main Program relative to the installed location</param>
/// <returns></returns>
public async Task ExecuteMainProgramFromFileAsync(string path);

```
#### Example: Execute a Console Main Program

```csharp

var code =
@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello Desktop Bridge!"");
            Console.ReadLine();
        }
    }
}";

await DesktopBridgeExtension.Instance.ExecuteMainProgramAsync(code);

```

### Executing Code Scripts

```csharp
/// <summary>
/// Executes the Code Script
/// </summary>
/// <param name="code">The code of the Script</param>
/// <returns></returns>
public async Task ExecuteScriptAsync(string code);

/// <summary>
/// Executes the Code Script
/// </summary>
/// <param name="path">The path to a file containing the code of the Script relative to the installed location</param>
/// <returns></returns>
public async Task ExecuteScriptFromFileAsync(string path);

/// <summary>
/// Executes the Code Script and returns a result value
/// </summary>
/// <typeparam name="TResult">The Type of the expected Result</typeparam>
/// <param name="code">The code of the Script</param>
/// <returns>The evaluated result</returns>
public async Task<TResult> ExecuteScriptAsync<TResult>(string code);

/// <summary>
/// Executes the Code Script and returns a result value
/// </summary>
/// <typeparam name="TResult">The Type of the expected Result</typeparam>
/// <param name="path">The path to a file containing the code of the Script relative to the installed location</param>
/// <returns>The evaluated result</returns>
public async Task<TResult> ExecuteScriptFromFileAsync<TResult>(string path);
```

Additional informations can be passed with the Script by using the following set of Fluent APIs.

#### Passing Parameters to a Script

```csharp

/// <summary>
/// Adds a Parameter to be passed as a part of the Script
/// </summary>
/// <typeparam name="TParam">The Type of the Parameter</typeparam>
/// <param name="paramName">Name of the Parameter</param>
/// <param name="paramValue">Value of the Parameter</param>
/// <returns></returns>
public DesktopBridgeExtension WithParameter<TParam>(string paramName, TParam paramValue);

```

#### Passing Using directives to a Script

```csharp

/// <summary>
/// Adds a Using directive to be passed as a part of the Script
/// </summary>
/// <param name="using">The using namespace (without the using keyword)</param>
/// <returns></returns>
public DesktopBridgeExtension WithUsing(string @using);

/// <summary>
/// Adds a collection of Using directives to be passed as a part of the Script
/// </summary>
/// <param name="usings">A collection of using namespaces (without the using keyword)</param>
/// <returns></returns>
public DesktopBridgeExtension WithUsing(IEnumerable<string> usings);

```

#### Passing Assembly References to a Script

```csharp

/// <summary>
/// Adds an assembly path to be referenced by the Script
/// </summary>
/// <param name="referencePath">Path to the assembly relative to the project folder</param>
/// <returns></returns>
public DesktopBridgeExtension WithReference(string referencePath);

/// <summary>
/// Adds a collection of assembly paths to be referenced by the Script
/// </summary>
/// <param name="referencePaths">Paths to the assemblies relative to the installed location</param>
/// <returns></returns>
public DesktopBridgeExtension WithReference(IEnumerable<string> referencePaths);

```

#### Example 1: Start a new Process passing its executable path

```csharp

var code =
@"Process cmd = new Process
  {
    StartInfo = { FileName = path }
  };
  cmd.Start();";

await DesktopBridgeExtension.Instance.WithParameter<string>("path", "myExe.exe").
                                      ExecuteScriptAsync(code);

```

#### Example 2: Show a Notify Icon in the Taskbar

```csharp

var code =
@"var notifyIcon = new NotifyIcon
  {
    BalloonTipText = @""Hello, NotifyIcon!"",
    Text = @""Hello, NotifyIcon!"",
    Icon = new Icon(""NotifyIcon.ico""),
    Visible = true
  };
  notifyIcon.ShowBalloonTip(1000);";

await DesktopBridgeExtension.Instance.WithUsing("System.Drawing").
                                      WithUsing("System.Windows").
                                      WithUsing("System.Windows.Forms").
                                      WithUsing("System.Diagnostics").
                                      WithUsing("System.Runtime.InteropServices").
                                      ExecuteScriptAsync(code);
```
