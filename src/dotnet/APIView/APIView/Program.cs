using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace APIView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var compilation = AssemblyAPIV.GetCompilation(args[0]);
                foreach (var assemblySymbol in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    if (assemblySymbol.Name.Equals("TestLibrary"))
                        Console.WriteLine(TreeRendererAPIV.RenderText(new AssemblyAPIV(assemblySymbol)));
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
