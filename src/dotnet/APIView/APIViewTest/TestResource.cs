using ApiView;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace APIViewTest
{
    public class TestResource
    {
        public static IAssemblySymbol GetAssemblySymbol()
        {
            return AssemblyApiView.GetCompilation("TestLibrary.dll");
        }

        public static object GetTestMember(string typeName, string memberName = null)
        {
            var compilation = AssemblyApiView.GetCompilation("TestLibrary.dll");

            if (memberName != null)
                return compilation.GetTypeByMetadataName(typeName).GetMembers(memberName).Single();
            else
                return compilation.GetTypeByMetadataName(typeName);
        }
    }
}
