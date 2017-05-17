using System.Collections.Generic;

namespace DesktopBridge.Extension.Shared.Models
{
    public class CompilationResult
    {
        public bool Success { get; set; }
        public List<Diagnostic> Diagnostics { get; set; }
    }
}