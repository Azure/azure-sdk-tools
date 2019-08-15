using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ApiView
{
    /// <summary>
    /// Class representing a C# assembly. Each assembly has a name and global namespace, 
    /// which may or may not contain further types.
    /// </summary>
    public class AssemblyApiView
    {
        public string Name { get; set; }
        public NamespaceApiView GlobalNamespace { get; set; }

        public AssemblyApiView() { }

        /// <summary>
        /// Construct a new AssemblyApiView instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the assembly.</param>
        public AssemblyApiView(IAssemblySymbol symbol)
        {
            this.Name = symbol.Name;
            this.GlobalNamespace = new NamespaceApiView(symbol.GlobalNamespace);
        }

        public static AssemblyApiView AssemblyFromFile(string dllPath)
        {
            using (var fileStream = File.OpenRead(dllPath))
            {
                return AssemblyFromStream(fileStream);
            }
        }

        public static AssemblyApiView AssemblyFromStream(Stream stream)
        {
            var compilation = GetCompilation(stream);

            return new AssemblyApiView(compilation);
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
            var renderer = new TextRendererApiView();
            var lines = renderer.Render(this);
            return lines.ToString();
        }
    }
}
