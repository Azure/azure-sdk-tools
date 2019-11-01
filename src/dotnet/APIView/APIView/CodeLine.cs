using APIView;

namespace ApiView
{
    public readonly struct CodeLine
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
    }
}
