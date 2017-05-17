using System.Collections.Generic;
using System.Linq;
using DesktopBridge.Extension.Shared.Models;

namespace DesktopBridge.Extension.Shared.Extensions
{
    internal static class DiagnosticExtension
    {
        internal static bool HasErrors(this IEnumerable<Diagnostic> diagnostics)
        {
            return diagnostics.Any(diagnostic => diagnostic.Kind == DiagnosticKind.Error);
        }
    }
}