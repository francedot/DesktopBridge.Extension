using System;

namespace DesktopBridge.Extension.Shared.Exceptions
{
    public class RuntimeException : Exception
    {
        public RuntimeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}