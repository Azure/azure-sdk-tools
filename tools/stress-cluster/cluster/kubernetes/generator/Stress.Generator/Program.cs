using System;

namespace Stress.Generator
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("This program will help generate a stress test package.");
            Console.WriteLine("CAUTION: This program may overwrite existing files.");

            var generator = new Generator();
            var package = generator.GenerateResource<StressTestPackage>();
            package.Write();
        }
    }
}
