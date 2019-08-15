using Microsoft.CodeAnalysis;
using ApiView;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace APIViewTest
{
    public class EventTests
    {
        [Fact]
        public void EventTestCreation()
        {
            var eventSymbol = (IEventSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEvent");
            var e = new EventApiView(eventSymbol);

            Assert.Equal("PublicEvent", e.Name);
        }

        [Fact]
        public void EventTestStringRep()
        {
            var eventSymbol = (IEventSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEvent");
            var e = new EventApiView(eventSymbol);

            Assert.Equal("public event System.EventHandler PublicEvent;", e.ToString());
        }

        [Fact]
        public void EventTestHTMLRender()
        {
            var e = new EventApiView
            {
                Accessibility = "public",
                Name = "TestEvent",
                Type = new TypeReferenceApiView(new TokenApiView[] { new TokenApiView() })
            };
            var renderer = new HTMLRendererApiView();
            var list = new StringListApiView();
            renderer.Render(e, list);
            Assert.Equal("<span class=\"keyword\">public</span> <span class=\"keyword\">event</span> " +
                "<span class=\"keyword\"></span> <a id=\"\" class=\"name commentable\">TestEvent</a>;", list.ToString());
        }
    }
}
