namespace ApiView
{
    public class LineApiView
    {
        public string DisplayString { get; set; }
        public string ElementId { get; set; }

        public LineApiView(string html)
        {
            this.DisplayString = html;
        }

        public LineApiView(string html, string id)
        {
            this.DisplayString = html;
            this.ElementId = id;
        }
    }
}
