[StructLayout(LayoutKind.Sequential)]
private struct FLASHWINFO
{
    public UInt32 cbSize;
    public IntPtr hwnd; // A Handle to the Window

    public UInt32 dwFlags; // Flash Status            
    public UInt32 uCount; // Number of times to flash the window            
    public UInt32 dwTimeout; // The rate in ms at which the Window is to be flashed
}

var hWnd = IntPtr.Zero;
foreach (Process pList in Process.GetProcesses())
{
    if (pList.MainWindowTitle == "DesktopBridge.Extension.SampleApp")
    {
        hWnd = pList.MainWindowHandle;
    }
}

const UInt32 FLASHW_TRAY = 2; // Const to flash the taskbar button
const UInt32 FLASHW_ALL = 3; // Const to flash both the window caption and taskbar button
const UInt32 FLASHW_TIMER = 4; // Const to flash continuously, until the FLASHW_STOP flag is set

[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

FLASHWINFO info = new FLASHWINFO
{
    hwnd = hWnd,
    dwFlags = FLASHW_ALL | FLASHW_TIMER,
    uCount = FLASHW_TRAY,
    dwTimeout = 0
};

info.cbSize = Convert.ToUInt32(Marshal.SizeOf(info));
FlashWindowEx(ref info);