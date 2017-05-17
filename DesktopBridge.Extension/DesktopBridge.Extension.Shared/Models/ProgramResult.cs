using System;

namespace DesktopBridge.Extension.Shared.Models
{
    public class ProgramResult
    {
        public CompilationResult CompilationResult { get; set; }
        public Exception Exception { get; set; }
        public string ReturnTypeName { get; set; }
        public string Result { get; set; }

        public bool Success => Exception == null;
        public bool HasReturnType => !string.IsNullOrEmpty(ReturnTypeName);
    }
}