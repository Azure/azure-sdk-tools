using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TypeList
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // create a compilation so the assembly semantics can be analyzed
                var reference = MetadataReference.CreateFromFile(args[0]);
                var compilation = CSharpCompilation.Create(null).AddReferences(reference);

                // analyze the provided assembly, and create a unique TypeData instance if this DLL hasn't been given already
                foreach (var assemblySymbol in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    Assembly data = new Assembly(assemblySymbol);
                    TreeRenderer tr = new TreeRenderer();
                    Console.WriteLine(tr.Render(data));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
