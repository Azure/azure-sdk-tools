using APIView;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace APIViewTest
{
    public class TestResource
    {
        public static IAssemblySymbol GetAssemblySymbol()
        {
            var compilation = AssemblyAPIV.GetCompilation("TestLibrary.dll");
            foreach (var assemblySymbol in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (assemblySymbol.Name.Equals("TestLibrary"))
                    return assemblySymbol;
            }

            return null;
        }

        public static object GetTestMember(string typeName, string memberName = null)
        {
            var compilation = AssemblyAPIV.GetCompilation("TestLibrary.dll");

            if (memberName != null)
                return compilation.GetTypeByMetadataName(typeName).GetMembers(memberName).Single();
            else
                return compilation.GetTypeByMetadataName(typeName);
        }
    }
}
