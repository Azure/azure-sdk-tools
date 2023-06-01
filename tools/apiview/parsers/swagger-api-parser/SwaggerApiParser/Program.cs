using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Linq;
using SwaggerApiParser.SwaggerApiView;
using SwaggerApiParser.Specs;

namespace SwaggerApiParser
{
    internal class Program
    {
        static Task<int> Main(string[] args)
        {
            var swaggers = new Option<IEnumerable<string>>("--swaggers",
                "The input swagger file. Can be a single file or multiple files separated by space.");

            var output = new Option<string>(name: "--output", description: "The output file path.",
                getDefaultValue: () => "swagger.json");

            var readmeFile = new Option<string>(name: "--readme", "The input readme file.");

            var readmeTag = new Option<string>(name: "--tag", description: "Readme tag used to generate swagger apiView",
                getDefaultValue: () => "default");

            var packageName = new Option<string>(name: "--package-name",
                description: "The package name for the generated code file.",
                getDefaultValue: () => "swagger");


            var swaggerLinks = new Option<IEnumerable<string>>(name: "--swaggerLinks",
                description: "The input swagger links. Can be a single URL or multiple URLs separated by space.", getDefaultValue:
                () => new List<string>()) {AllowMultipleArgumentsPerToken = true};

            var cmd = new RootCommand
            {
                swaggers,
                output,
                packageName,
                swaggerLinks,
                readmeFile,
                readmeTag
            };


            cmd.Description = "Parse swagger file into codefile.";

            cmd.SetHandler(async (IEnumerable<string> swaggerFiles, string outputFile, string package, IEnumerable<string> links, string readme, string tag) =>
            {
                var swaggerLinksArray = links.ToList();

                var enumerable = swaggerFiles as string[] ?? swaggerFiles.ToArray();

                if (readme != null)
                {
                    readme = Path.GetFullPath(readme);
                }
                await HandleGenerateCodeFile(enumerable, outputFile, package, swaggerLinksArray, readme, tag);
            }, swaggers, output, packageName, swaggerLinks, readmeFile, readmeTag);

            return Task.FromResult(cmd.Invoke(args));
        }

        static async Task HandleGenerateCodeFile(IEnumerable<string> swaggers, string output, string packageName, List<string> swaggerLinks, string readmeFile, string readmeTag)
        {
          
            var swaggerFilePaths =  swaggers.ToList();
            if (readmeFile != null)
            {
                var readmeFileDir = Path.GetDirectoryName(readmeFile);
                var swaggerFiles = ReadmeParser.GetSwaggerFilesFromReadme(readmeFile, readmeTag);
                swaggerFilePaths = swaggerFilePaths.Concat(swaggerFiles.Select(it => Path.Join(readmeFileDir, it))).ToList();
            }
            
            if (!swaggerFilePaths.Any())
            {
                Console.WriteLine("No swagger files specified. For usage, run with --help");
                return;
            }
            
            
            SwaggerApiViewRoot root = new SwaggerApiViewRoot(packageName, packageName);

            List<Swagger> swaggerSpecs = new List<Swagger>();

            var idx = 0;
            foreach (var swaggerFilePath in swaggerFilePaths)
            {
                if (!File.Exists(swaggerFilePath))
                {
                    // Suppressing this error when swagger file is missing or path is incorrect
                    // Ideally any incorrect swagger file path reference in README should be caught by validation tool
                    Console.WriteLine($"Invalid file path to swagger file. Skipping {swaggerFilePath}.");
                    continue;
                }

                var swaggerLink = idx < swaggerLinks.Count ? swaggerLinks[idx] : "";
                idx++;

                var input = Path.GetFullPath(swaggerFilePath);
                Console.WriteLine("Generating codefile for swagger file: {0}", Path.GetFileName(input));
                var swaggerSpec = await SwaggerDeserializer.Deserialize(input);
                swaggerSpec.swaggerFilePath = Path.GetFullPath(swaggerFilePath);
                swaggerSpec.swaggerLink = swaggerLink;
                swaggerSpecs.Add(swaggerSpec);
                root.AddDefinitionToCache(swaggerSpec, swaggerSpec.swaggerFilePath);                
            }

            foreach (var swaggerSpec in swaggerSpecs)
            {
                await root.AddSwaggerSpec(swaggerSpec, swaggerSpec.swaggerFilePath, packageName, swaggerSpec.swaggerLink);
            }

            var codeFile = root.GenerateCodeFile();
            var outputFilePath = Path.GetFullPath(output);
            await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
            Console.WriteLine($"Generate codefile {output} successfully.");
            await codeFile.SerializeAsync(writer);
        }
    }
}
