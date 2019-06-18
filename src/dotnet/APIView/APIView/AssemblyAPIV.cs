using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APIView
{
    /// <summary>
    /// Class representing a C# assembly. Each assembly has a name and global namespace, 
    /// which may or may not contain further types.
    /// 
    /// AssemblyAPIV is an immutable, thread-safe type.
    /// </summary>
    public class AssemblyAPIV
    {
        public string Name { get; }
        public NamespaceAPIV GlobalNamespace { get; }

        /// <summary>
        /// Construct a new AssemblyAPIV instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the assembly.</param>
        public AssemblyAPIV(IAssemblySymbol symbol)
        {
            this.Name = symbol.Name;
            this.GlobalNamespace = new NamespaceAPIV(symbol.GlobalNamespace);
        }

        public static List<AssemblyAPIV> AssembliesFromFile(string dllPath)
        {
            var compilation = GetCompilation(dllPath);
            List<AssemblyAPIV> assemblies = new List<AssemblyAPIV>();

            foreach (var assemblySymbol in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                assemblies.Add(new AssemblyAPIV(assemblySymbol));
            }

            return assemblies;
        }

        public static CSharpCompilation GetCompilation(string dllPath)
        {
            var reference = MetadataReference.CreateFromFile(dllPath);
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            foreach (var tpl in trustedAssemblies)
            {
                compilation = compilation.AddReferences(MetadataReference.CreateFromFile(tpl));
            }

            return compilation;
        }

        public override string ToString()
        {
            return TreeRendererAPIV.RenderText(this);
        }
    }
}
