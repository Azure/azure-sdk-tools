using APIView;
using System;

namespace ApiView
{
    public readonly struct CodeLine: IEquatable<CodeLine>
    {
        public string DisplayString { get; }
        public string ElementId { get; }
        public string LineClass { get; }
        public int IndentSize { get; }
        public bool IsDocumentation { get; }

        public CodeLine(string html, string id, string lineClass, int indentSize = 0, bool isDocumentation = false)
        {
            this.DisplayString = html;
            this.ElementId = id;
            this.LineClass = lineClass;
            this.IndentSize = indentSize;
            this.IsDocumentation = isDocumentation;
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
