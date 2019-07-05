using Microsoft.CodeAnalysis;
using APIView;
using Xunit;
using System.Text;

namespace APIViewTest
{
    public class EventTests
    {
        [Fact]
        public void EventTestCreation()
        {
            var eventSymbol = (IEventSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEvent");
            var e = new EventAPIV(eventSymbol);

            Assert.Equal("PublicEvent", e.Name);
        }

        [Fact]
        public void EventTestStringRep()
        {
            var eventSymbol = (IEventSymbol)TestResource.GetTestMember("TestLibrary.PublicClass", "PublicEvent");
            var e = new EventAPIV(eventSymbol);

            Assert.Equal("public event EventHandler PublicEvent;", e.ToString());
        }

        [Fact]
        public void EventTestHTMLRender()
        {
            var e = new EventAPIV
            {
                Accessibility = "public",
                Name = "TestEvent"
            };
            var builder = new StringBuilder();
            var renderer = new HTMLRendererAPIV();
            renderer.Render(e, builder);
            Assert.Equal("<span class=\"keyword\">public</span> <span class=\"specialName\">event</span> <span class=\"class\">" +
                "EventHandler</span> <span class=\"name\">TestEvent</span>;", builder.ToString());
        }
    }
}
