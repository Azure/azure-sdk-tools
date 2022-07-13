using APIView;
using System;

namespace ApiView
{
    public readonly struct CodeLine: IEquatable<CodeLine>
    {
        public string DisplayString { get; }
        public string ElementId { get; }
        public string LineClass { get; }
        public int? LineNumber { get; }
        public int IndentSize { get; }
        public int? SectionKey { get;  }

        public CodeLine(string html, string id, string lineClass, int? lineNumber = null, int indentSize = 0, int? sectionKey = null)
        {
            this.DisplayString = html;
            this.ElementId = id;
            this.LineClass = lineClass;
            this.LineNumber = lineNumber;
            this.IndentSize = indentSize;
            this.SectionKey = sectionKey;
        }

        public override string ToString()
        {
            return DisplayString + " [" + ElementId + "]";
        }

        public bool Equals(CodeLine other)
        {
            return DisplayString == other.DisplayString && ElementId == other.ElementId;
        }
    }
}
