using System.Diagnostics;
using System.Windows;

namespace DesktopBridge.Extension.Proxy.App
{
    public partial class App : Application
    {
        public App() : base()
        {
            var curProc = Process.GetCurrentProcess();
            var curProcName = curProc.ProcessName;
            var processes = Process.GetProcessesByName(curProcName);
            foreach (var process in processes)
            {
                if (process.Id != curProc.Id)
                {
                    process.Kill();
                }
            }
        }
    }
}
