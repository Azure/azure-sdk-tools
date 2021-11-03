using System;
using System.IO;
using CommandLine;

namespace Stress.Generator
{
    public class Program
    {
        public class Options
        {
            [Option('d', "directory", Required = false, HelpText = "Output directory for stress test.")]
            public string OutputDirectory { get; set; } = Directory.GetCurrentDirectory();
        }

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                Console.WriteLine("This program will help generate a stress test package.");
                Console.WriteLine("CAUTION: This program may overwrite existing files.");

                var outdir = Path.GetFullPath(o.OutputDirectory);

                var generator = new Generator();
                var package = generator.GenerateResource<StressTestPackage>();

                if (!Directory.Exists(outdir))
                {
                    Directory.CreateDirectory(outdir);
                }
                Directory.SetCurrentDirectory(outdir);

                foreach (var f in Directory.GetFiles(outdir))
                {
                    Console.WriteLine(f);
                }

                package.Write();

                Console.WriteLine($"Stress test created at {outdir}");
            });
        }
    }
}
