using System;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    public class NavigationItem
    {
        public string Text { get; set; }
        public string[] Children { get; set; } = Array.Empty<string>();

        public void Add(string child)
        {
            var list = new List<string>(Children);
            list.Add(child);
            Children = list.ToArray();
        }

        public override string ToString() => Text.ToString();
    }
}
