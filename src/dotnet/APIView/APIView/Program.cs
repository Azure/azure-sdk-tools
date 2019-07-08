using System;

namespace APIView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var renderer = new TextRendererAPIV();
                Console.WriteLine(renderer.Render(AssemblyAPIV.AssemblyFromFile(args[0])));

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
