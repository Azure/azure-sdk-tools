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

                Console.WriteLine($"********************************************************************************");
                Console.WriteLine($"Stress test package created at {outdir}. See README.md in that directory for more help.");
                Console.WriteLine($"Next steps:");
                Console.WriteLine($"1. Install stress test development tools: https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md#installation");
                Console.WriteLine($"2. Add test code to ./src/");
                Console.WriteLine($"3. Update 'Dockerfile' (see contents for help).");
                Console.WriteLine($"4. Run the following command from within your language repository to deploy the package:");
                Console.WriteLine($"   pwsh -c $(git rev-parse --show-toplevel)/eng/common/scripts/stress-testing/deploy-stress-tests.ps1");
                Console.WriteLine($"********************************************************************************");
            });
        }
    }
}
