using System;

namespace ApiView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var assemblySymbol = CompilationFactory.GetCompilation(args[0]);
                var renderer = new CodeFileRenderer();
                var codeNode = new CodeFileBuilder().Build(assemblySymbol, false);
                Console.WriteLine(renderer.Render(codeNode));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
