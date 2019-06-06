using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TypeList
{
    /// <summary>
    /// Class representing a C# assembly. Each assembly has a name and global namespace, 
    /// which may or may not contain further types.
    /// 
    /// Assembly is an immutable, thread-safe type.
    /// </summary>
    public class Assembly
    {
        private readonly string name;
        private Namespace globalNamespace;

        /// <summary>
        /// Construct a new Assembly instance, represented by the provided symbol.
        /// </summary>
        /// <param name="symbol">The symbol representing the assembly.</param>
        public Assembly(IAssemblySymbol symbol)
        {
            this.name = symbol.Name;
            this.globalNamespace = new Namespace(symbol.GlobalNamespace);
        }

        public static Collection<Assembly> AssembliesFromFile(string dllPath)
        {
            var reference = MetadataReference.CreateFromFile(dllPath);
            var compilation = CSharpCompilation.Create(null).AddReferences(reference);
            Collection<Assembly> assemblies = new Collection<Assembly>();

            foreach (var assemblySymbol in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                assemblies.Add(new Assembly(assemblySymbol));
            }

            return assemblies;
        }

        public string GetName()
        {
            return name;
        }

        public Namespace GetGlobalNamespace()
        {
            return globalNamespace;
        }

        public override string ToString()
        {
            return "Assembly: " + name + "\n\n" + globalNamespace.ToString();
        }
    }
}
