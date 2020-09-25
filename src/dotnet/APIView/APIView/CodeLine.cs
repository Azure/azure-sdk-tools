using APIView;
using System;

namespace ApiView
{
    public readonly struct CodeLine: IEquatable<CodeLine>
    {
        public string DisplayString { get; }
        public string ElementId { get; }

        public CodeLine(string html, string id)
        {
            this.DisplayString = html;
            this.ElementId = id;
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
