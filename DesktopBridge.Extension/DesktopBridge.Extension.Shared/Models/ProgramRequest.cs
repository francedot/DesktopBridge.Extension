using System.Collections.Generic;

namespace DesktopBridge.Extension.Shared.Models
{
    public class ProgramRequest
    {
        public string Code { get; set; }
        public string ReturnTypeName { get; set; }
        public ProgramKind ProgramKind { get; set; }
        public List<Parameter> Parameters { get; set; }
        public List<string> Usings { get; set; }
        public List<string> References { get; set; }

        public bool HasReturnType => !string.IsNullOrEmpty(ReturnTypeName);
    }
}