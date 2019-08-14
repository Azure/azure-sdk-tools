using System;

namespace ApiView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var renderer = new TextRendererApiv();
                Console.WriteLine(renderer.Render(AssemblyApiv.AssemblyFromFile(args[0])));

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
