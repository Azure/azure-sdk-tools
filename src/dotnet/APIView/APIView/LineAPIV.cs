using System;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    public class LineAPIV
    {
        public string DisplayString { get; set; }
        public string ElementId { get; set; }

        public LineAPIV(string html)
        {
            this.DisplayString = html;
        }

        public LineAPIV(string html, string id)
        {
            this.DisplayString = html;
            this.ElementId = id;
        }
    }
}
