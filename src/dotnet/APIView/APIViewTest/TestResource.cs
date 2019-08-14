using ApiView;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace APIViewTest
{
    public class TestResource
    {
        public static IAssemblySymbol GetAssemblySymbol()
        {
            return AssemblyApiv.GetCompilation("TestLibrary.dll");
        }

        public static object GetTestMember(string typeName, string memberName = null)
        {
            var compilation = AssemblyApiv.GetCompilation("TestLibrary.dll");

            if (memberName != null)
                return compilation.GetTypeByMetadataName(typeName).GetMembers(memberName).Single();
            else
                return compilation.GetTypeByMetadataName(typeName);
        }
    }
}
