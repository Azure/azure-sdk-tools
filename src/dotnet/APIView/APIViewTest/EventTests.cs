using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using APIView;
using Xunit;

namespace APIViewTest
{
    public class EventTests
    {
        [Fact]
        public void EventTestCreation()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var eventSymbol = (IEventSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("PublicEvent").Single();
            var e = new EventAPIV(eventSymbol);

            Assert.Equal("PublicEvent", e.Name);

            Assert.Contains("public event EventHandler PublicEvent;", e.ToString());
        }

        [Fact]
        public void EventTestStringRep()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var eventSymbol = (IEventSymbol)a.GetTypeByMetadataName("TestLibrary.PublicClass").GetMembers("PublicEvent").Single();
            var e = new EventAPIV(eventSymbol);

            Assert.Contains("public event EventHandler PublicEvent;", e.ToString());
        }
    }
}
