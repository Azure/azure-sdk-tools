using System;

namespace APIView
{
    public class NavigationItem
    {
        public string Text { get; set; }
        public string NavigationId { get; set; }
        public NavigationItem[] ChildItems { get; set; } = Array.Empty<NavigationItem>();
        
        public override string ToString() => Text;
    }
}
