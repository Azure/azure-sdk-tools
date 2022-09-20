using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Linq;

namespace swagger_api_parser
{
    internal class Program
    {
        static Task<int> Main(string[] args)
        {
            var swaggers = new Argument<IEnumerable<string>>("swaggers",
                "The input swaggers file. Can be a single file or multiple files separated by space.");

            var output = new Option<string>(name: "--output", description: "The output file path.",
                getDefaultValue: () => "swagger.json");

            var packageName = new Option<string>(name: "--package-name",
                description: "The package name for the generated code file.",
                getDefaultValue: () => "swagger");

            var cmd = new RootCommand {swaggers, output, packageName};

            cmd.Description = "Parse swagger file into codefile.";

            cmd.SetHandler(async (IEnumerable<string> swaggerFiles, string outputFile, string package) =>
            {
                var enumerable = swaggerFiles as string[] ?? swaggerFiles.ToArray();
                if (!enumerable.Any())
                {
                    Console.WriteLine("No swagger files specified. For usage, run with --help");
                    return;
                }

                await HandleGenerateCodeFile(enumerable, outputFile, package);
            }, swaggers, output, packageName);

            return Task.FromResult(cmd.Invoke(args));
        }

        static async Task HandleGenerateCodeFile(IEnumerable<string> swaggers, string output, string packageName)
        {
            var swaggerFilePaths = swaggers as string[] ?? swaggers.ToArray();
            var swaggerCodeFileRender = new SwaggerCodeFileMerger();
            foreach (var swaggerFilePath in swaggerFilePaths)
            {
                if (!File.Exists(swaggerFilePath))
                {
                    throw new FileNotFoundException(swaggerFilePath);
                }

                var input = Path.GetFullPath(swaggerFilePath);
                var swaggerFileName = Path.GetFileName(input);
                Console.WriteLine("Input swagger file: {0}", input);
                await using FileStream fileReadStream = File.OpenRead(input);
                var ls = new SwaggerTokenSerializer();
                var cf = await ls.GetCodeFileInternalAsync(swaggerFileName, fileReadStream, false);
                swaggerCodeFileRender.AppendResult(swaggerFileName, cf);
            }

            await swaggerCodeFileRender.GenerateCodeFile(output, packageName);
        }
    }
}
