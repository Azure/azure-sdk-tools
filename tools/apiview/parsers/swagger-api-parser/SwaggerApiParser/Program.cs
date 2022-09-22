using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Linq;

namespace SwaggerApiParser
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
            SwaggerApiViewRoot root = new SwaggerApiViewRoot(packageName, packageName);
            foreach (var swaggerFilePath in swaggerFilePaths)
            {
                if (!File.Exists(swaggerFilePath))
                {
                    throw new FileNotFoundException(swaggerFilePath);
                }
                var input = Path.GetFullPath(swaggerFilePath);
                Console.WriteLine("Generating codefile for swagger file: {0}", Path.GetFileName(input));
                var swaggerSpec = await SwaggerDeserializer.Deserialize(input);
                root.AddSwaggerSpec(swaggerSpec, Path.GetFullPath(input), packageName);
            }

            var codeFile = root.GenerateCodeFile();
            var outputFilePath = Path.GetFullPath(output);
            await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
            Console.WriteLine($"Generate codefile {output} successfully.");
            await codeFile.SerializeAsync(writer);
        }
    }
}
