using System;

namespace DesktopBridge.Extension.Shared.Exceptions
{
    public class ProgramExecutionException : Exception
    {
        public ProgramExecutionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}