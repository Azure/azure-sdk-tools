using System;
using System.Collections.Generic;

namespace APIView
{
    public class NavigationItem
    {
        public string Text { get; set; }

        public string NavigationId { get; set; }

        public NavigationItem[] ChildItems { get; set; } = Array.Empty<NavigationItem>();

        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(0);

        public bool IsHiddenApi { get; set; }

        public override string ToString() => Text;
    }
}
