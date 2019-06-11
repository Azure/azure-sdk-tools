using System;
using System.Collections;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TypeList
{
    class Program
    {
        static void Main(string[] args)
        {
            Program program = new Program();
            Console.WriteLine("Type 'quit' to exit");
            Console.WriteLine();
            while (true)
            {
                Console.Write("DLL path: ");
                string input = Console.ReadLine();

                if (input == "quit")
                {
                    break;
                }
                else if (input.Length > 0)
                {
                    Console.WriteLine();
                    program.List(input);
                    Console.WriteLine();
                }
            }
        }

        private void List(string dllPath)
        {
            try
            {
                // create a compilation so the assembly semantics can be analyzed
                var reference = MetadataReference.CreateFromFile(dllPath);
                var compilation = CSharpCompilation.Create(null).AddReferences(reference);

                // analyze the provided assembly, and create a unique TypeData instance if this DLL hasn't been given already
                foreach (var assemblySymbol in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    Assembly data = new Assembly(assemblySymbol);
                    Console.WriteLine(data.RenderAssembly());
                }
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
