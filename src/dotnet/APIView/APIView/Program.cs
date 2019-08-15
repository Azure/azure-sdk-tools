using System;

namespace ApiView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var renderer = new TextRendererApiView();
                Console.WriteLine(renderer.Render(AssemblyApiView.AssemblyFromFile(args[0])));

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
