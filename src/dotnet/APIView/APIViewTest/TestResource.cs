using APIView;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace APIViewTest
{
    public class TestResource
    {
        public static IAssemblySymbol GetAssemblySymbol()
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            foreach (var tpl in trustedAssemblies)
            {
                compilation = compilation.AddReferences(MetadataReference.CreateFromFile(tpl));
            }
            foreach (var assemblySymbol in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (assemblySymbol.Name.Equals("TestLibrary"))
                    return assemblySymbol;
            }

            return null;
        }

        public static object GetTestMember(string typeName, string memberName = null)
        {
            var reference = MetadataReference.CreateFromFile("TestLibrary.dll");
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var a = compilation.SourceModule.ReferencedAssemblySymbols[0];

            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            foreach (var tpl in trustedAssemblies)
            {
                compilation = compilation.AddReferences(MetadataReference.CreateFromFile(tpl));
            }

            if (memberName != null)
                return a.GetTypeByMetadataName(typeName).GetMembers(memberName).Single();
            else
                return a.GetTypeByMetadataName(typeName);
        }
    }
}
