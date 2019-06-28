using System;

namespace APIView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var compilation = AssemblyAPIV.GetCompilation(args[0]);
                Console.WriteLine(TreeRendererAPIV.RenderText(new AssemblyAPIV(compilation)));

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
