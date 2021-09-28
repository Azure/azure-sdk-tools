using System;

namespace Stress.Generator
{
    public class Program
    {
        public static void Main()
        {
            var generator = new Generator();
            var resources = generator.GenerateResources();

            Console.WriteLine("Done");
        }
    }
}