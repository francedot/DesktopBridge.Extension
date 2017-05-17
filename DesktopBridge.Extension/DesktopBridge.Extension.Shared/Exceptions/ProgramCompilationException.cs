using System;
using System.Collections.Generic;
using DesktopBridge.Extension.Shared.Models;

namespace DesktopBridge.Extension.Shared.Exceptions
{
    public class ProgramCompilationException : Exception
    {
        public ProgramCompilationException(string message, List<Diagnostic> diagnostics) : base(message)
        {
            Diagnostics = diagnostics;
        }

        public List<Diagnostic> Diagnostics { get; }
    }
}