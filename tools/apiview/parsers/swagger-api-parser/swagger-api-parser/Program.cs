using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Linq;
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

        public async Task GenerateCodeFile(string outputFile)
        {
            CodeFile result = new CodeFile
            {
                Language = "Swagger", VersionString = "0", Name = outputFile, PackageName = outputFile,
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
                    LongText = null,
                    NavigationId = $"{swaggerFileName}_-swagger",
                    ChildItems = codeFile.Navigation.Select(it => it).ToArray()
                };
                result.Navigation = result.Navigation.Concat(new NavigationItem[] { navigation }).ToArray();
            }

            Console.WriteLine($"Writing {outputFile}");
            var outputFilePath = Path.GetFullPath(outputFile);
            await using ( var fileWriteStream = File.Open(outputFilePath, FileMode.Create))
            {
                await result.SerializeAsync(fileWriteStream);
            }
            Console.WriteLine("finished");
        }
    }


    internal class Program
    {
        static Task<int> Main(string[] args)
        {
            var swaggers = new Argument<IEnumerable<string>>("swaggers",
                "The input swaggers file. Can be a single file or multiple files separated by space.");

            var output = new Option<string>(name: "--output", description: "The output file path.", getDefaultValue: ()=> "swagger.json");
            var cmd = new RootCommand {swaggers, output };

            cmd.Description = "Parse swagger file into codefile.";

            cmd.SetHandler(async (IEnumerable<string> swaggerFiles, string outputFile) =>
            {
                var enumerable = swaggerFiles as string[] ?? swaggerFiles.ToArray();
                if(!enumerable.Any())
                {
                    Console.WriteLine("No swagger files specified. For usage, run with --help");
                    return;
                }
                
                await HandleGenerateCodeFile(enumerable, outputFile);
            }, swaggers, output);
            
            return Task.FromResult(cmd.Invoke(args));

        }

        static async Task HandleGenerateCodeFile(IEnumerable<string> swaggers, string output)
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

            await swaggerCodeFileRender.GenerateCodeFile(output);
        }
    }
}
