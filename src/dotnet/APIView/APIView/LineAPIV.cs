namespace ApiView
{
    public class LineApiv
    {
        public string DisplayString { get; set; }
        public string ElementId { get; set; }

        public LineApiv(string html)
        {
            this.DisplayString = html;
        }

        public LineApiv(string html, string id)
        {
            this.DisplayString = html;
            this.ElementId = id;
        }
    }
}
