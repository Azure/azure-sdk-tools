using System;
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

        public static AssemblyAPIV AssemblyFromFile(string dllPath)
        {
            using (var fileStream = File.OpenRead(dllPath))
            {
                return AssemblyFromStream(fileStream);
            }
        }

        public static AssemblyAPIV AssemblyFromStream(Stream stream)
        {
            var compilation = GetCompilation(stream);

            return new AssemblyAPIV(compilation);
        }

        public static IAssemblySymbol GetCompilation(string dllPath)
        {
            using (var fileStream = File.OpenRead(dllPath))
            {
                return GetCompilation(fileStream);
            }
        }

        public static IAssemblySymbol GetCompilation(Stream stream)
        {
            var reference = MetadataReference.CreateFromStream(stream);
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            var corlibLocation = typeof(object).Assembly.Location;
            var runtimeFolder = Path.GetDirectoryName(corlibLocation);

            compilation = compilation.AddReferences(MetadataReference.CreateFromFile(corlibLocation));

            var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            foreach (var tpl in trustedAssemblies)
            {
                if (tpl.StartsWith(runtimeFolder))
                {
                    compilation = compilation.AddReferences(MetadataReference.CreateFromFile(tpl));
                }
            }

            return (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(reference);
        }

        public override string ToString()
        {
            return TreeRendererAPIV.RenderText(this);
        }
    }
}
