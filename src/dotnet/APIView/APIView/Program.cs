using System;

namespace APIView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(TreeRendererAPIV.RenderText(AssemblyAPIV.AssemblyFromFile(args[0])));

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
