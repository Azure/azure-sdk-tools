using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
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
            AssemblyAPIV assembly = AssemblyAPIV.AssembliesFromFile("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\" +
                "TestLibrary\\Debug\\netstandard2.0\\TestLibrary.dll")[0];
            Assert.Equal("TestLibrary", assembly.Name);

            NamespaceAPIV globalNamespace = assembly.GlobalNamespace;
            ImmutableArray<NamespaceAPIV> namespaces = globalNamespace.Namespaces;
            NamespaceAPIV testLibNamespace = namespaces[0];
            Assert.Equal("TestLibrary", testLibNamespace.Name);

            ImmutableArray<NamedTypeAPIV> classes = testLibNamespace.NamedTypes;
            NamedTypeAPIV publicClass = null;
            foreach (NamedTypeAPIV n in classes)
            {
                if (n.Name.Equals("PublicClass"))
                    publicClass = n;
            }
            Assert.NotNull(publicClass);
            Assert.Equal("PublicClass", publicClass.Name);

            ImmutableArray<EventAPIV> events = publicClass.Events;
            Assert.Single(events);
            Assert.Equal("PublicEvent", events[0].Name);

            Assert.Contains("public event EventHandler PublicEvent;", events[0].ToString());
        }
    }
}
