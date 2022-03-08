using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Linq;
using System.Text.RegularExpressions;
using ApiView;
using APIView;
using APIViewWeb;

namespace swagger_api_parser
{
    public class SwaggerCodeFileMerger
    {
        private readonly Dictionary<string, CodeFile> originalResult;

        public SwaggerCodeFileMerger()
        {
            this.originalResult = new Dictionary<string, CodeFile>();
        }

        public void AppendResult(string swaggerFileName, CodeFile cf)
        {
            if (this.originalResult.TryGetValue(swaggerFileName, out CodeFile existing))
            {
                Console.WriteLine($"{swaggerFileName} already exists.");
            }
            else
            {
                this.originalResult.Add(swaggerFileName, cf);
            }
        }

        static string GetResourceProviderFromPath(string path)
        {
            const string resourceProviderPattern = "/providers/(:?[^{/]+)";
            var match = Regex.Match(path, resourceProviderPattern, RegexOptions.RightToLeft);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static NavigationItem[] RebuildNavigation(IEnumerable<NavigationItem> navigation,
            string swaggerFileName)
        {
            var result = new List<NavigationItem>();

            var generalNavigationItem = new NavigationItem()
            {
                Text = "general", NavigationId = $"{swaggerFileName}_-swagger",
            };

            var operationIdNavigationItem = new NavigationItem()
            {
                Text = "operationIds", NavigationId = $"{swaggerFileName}_-swagger",
            };

            var pathNavigationItem = new NavigationItem() {Text = "paths", NavigationId = $"{swaggerFileName}_-paths",};

            var aggregatedPaths = new Dictionary<string, NavigationItem[]>();

            string[] generalKeys = new string[]
            {
                "swagger", "host", "info", "basePath", "schemes", "consumes", "produces", "securityDefinitions",
                "security", "x-ms-parameterized-host"
            };
            foreach (var item in navigation)
            {
                if (generalKeys.Contains(item.Text))
                {
                    generalNavigationItem.ChildItems =
                        generalNavigationItem.ChildItems.Concat(new NavigationItem[] {item}).ToArray();
                }
                else if (item.Text.Equals("paths"))
                {
                    // extract operationIds from path 
                    foreach (var path in item.ChildItems)
                    {
                        foreach (var operationIdItem in path.ChildItems)
                        {
                            operationIdNavigationItem.ChildItems =
                                operationIdNavigationItem.ChildItems.Concat(new NavigationItem[] {operationIdItem})
                                    .ToArray();
                        }

                        var resourceProvider = GetResourceProviderFromPath(path.Text);
                        path.ChildItems = Array.Empty<NavigationItem>();
                        if (resourceProvider == null)
                        {
                            // If there is no resource provider, add the path to the paths directly.
                            pathNavigationItem.ChildItems = pathNavigationItem.ChildItems
                                .Concat(new NavigationItem[] {path}).ToArray();
                            continue;
                        }

                        // For Azure management plane API the API path is too long to present, To resolve this issue, we need to add the path to the aggregated paths.
                        var index = path.Text.LastIndexOf(resourceProvider, StringComparison.Ordinal);
                        var apiPath = path.Text[..(index + resourceProvider.Length)];
                        if (aggregatedPaths.TryGetValue(apiPath, out NavigationItem[] existing))
                        {
                            aggregatedPaths[apiPath] = existing.Concat(new NavigationItem[] {path}).ToArray();
                        }
                        else
                        {
                            aggregatedPaths.Add(apiPath, new NavigationItem[] {path});
                        }
                    }
                }
                else
                {
                    result.Add(item);
                }
            }

            foreach (var (aggregatedPath, pathItems) in aggregatedPaths)
            {
                var parentNavigationItem = new NavigationItem()
                {
                    Text = aggregatedPath, NavigationId = $"{swaggerFileName}_-paths",
                };

                foreach (var path in pathItems)
                {
                    path.Text = path.Text[aggregatedPath.Length..];
                    parentNavigationItem.ChildItems =
                        parentNavigationItem.ChildItems.Concat(new NavigationItem[] {path}).ToArray();
                }

                pathNavigationItem.ChildItems = pathNavigationItem.ChildItems
                    .Concat(new NavigationItem[] {parentNavigationItem}).ToArray();
            }


            result.Insert(0, generalNavigationItem);
            result.Insert(1, pathNavigationItem);
            result.Insert(result.Count(), operationIdNavigationItem);
            return result.ToArray();
        }

        public async Task GenerateCodeFile(string outputFile, string packageName)
        {
            CodeFile result = new CodeFile
            {
                Language = "Swagger",
                VersionString = "0",
                Name = packageName,
                PackageName = packageName,
                Tokens = new CodeFileToken[] { },
                Navigation = new NavigationItem[] { }
            };


            foreach (var (swaggerFileName, codeFile) in this.originalResult)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(swaggerFileName);
                result.Tokens = result.Tokens.Concat(codeFile.Tokens).Select(it => it).ToArray();

                // Add file name as top level to navigation
                var navigation = new NavigationItem
                {
                    Text = swaggerFileName,
                    NavigationId = $"{swaggerFileName}_-swagger",
                    ChildItems = RebuildNavigation(codeFile.Navigation, swaggerFileName)
                };
                result.Navigation = result.Navigation.Concat(new NavigationItem[] {navigation}).ToArray();
            }

            Console.WriteLine($"Writing {outputFile}");
            var outputFilePath = Path.GetFullPath(outputFile);
            await using FileStream fileWriteStream = File.Open(outputFilePath, FileMode.Create);
            await result.SerializeAsync(fileWriteStream);
            Console.WriteLine("finished");
        }
    }


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
                var ls = new SwaggerLanguageService();
                var cf = await ls.GetCodeFileInternalAsync(swaggerFileName, fileReadStream, false);
                swaggerCodeFileRender.AppendResult(swaggerFileName, cf);
            }

            await swaggerCodeFileRender.GenerateCodeFile(output, packageName);
        }
    }
}
