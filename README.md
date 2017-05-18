## Desktop Bridge Extension Library for the UWP

The Desktop.Bridge Extension Library allows for Win32 APIs to be called from the UWP (x86, x64) abstracting the standard IPC (Inter Process Comunication) approach in favour of asynchronous APIs.

### Setup
* Reference DesktopBridge.Extension.Interface and Reference DesktopBridge.Extension.Shared from the UWP project.
* Modify UWP Project Package.appxmanifest:

  1. Add rescap and desktop namespaces: 
  ```xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"```
  ```xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"```
  2. Add ignorable namespaces: ```IgnorableNamespaces="uap mp rescap desktop"```
  3. Add Extension AppService for middleware comunication and register the embedded executable running full trust
```xml
<Extensions>
  <uap:Extension Category="windows.appService">
    <uap:AppService Name="DesktopBridgeMiddleware" />
  </uap:Extension>
  <desktop:Extension Category="windows.fullTrustProcess" Executable="desktop\DesktopBridge.Extension.Proxy.App.exe" />
</Extensions> 
```

See the [DesktopBridge.Extension.SampleApp](https://github.com/francedot/DesktopBridge.Extension/tree/master/DesktopBridge.Extension/DesktopBridge.Extension.SampleApp) app for further reference.

### API Usage

1. Make the App class inherit the **AppConnectionAware** abstracted class in order for the APIs to gain access to the DesktopBridgeMiddleware App Service.
2. In the App **OnLaunched**, call the base class implementation ```base.OnLaunched(e)``` . This is responsible for initializing the AppService and launching the registered full trust process.

Alternatively, make sure to obtain an instance of the DesktopBridgeMiddleware App Service by overriding the App OnBackgroundActivated and pass the connection to the **InflateConnection()** method of the **DesktopBridgeExtension** class. Then, call **InitializeAsync()** to start the full trust process.

The Win32 APIs are exposed through the **DesktopBridgeExtension** class via its singleton: ```DesktopBridgeExtension.Instance```

### Win32 Code Scripts

