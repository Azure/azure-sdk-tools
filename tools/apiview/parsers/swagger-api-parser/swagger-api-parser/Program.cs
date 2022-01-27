using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using System.Linq;
using System.Text.RegularExpressions;
using ApiView;
using APIViewWeb;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace swagger_api_parser
{
    public class SwaggerSpec
    {
        public string swagger { get; set;} 
        public Dictionary<string, object> paths {get; set;}
    }

    public class ParserResultProcessor
    {
        private Dictionary<string, CodeFile> result;

        public ParserResultProcessor()
        {
            this.result = new Dictionary<string, CodeFile>();
        }

        public void AppendResult(string resourceProvider, CodeFile cf)
        {
            if(this.result.TryGetValue(resourceProvider, out CodeFile existing))
            {
                var codeFileTokens = existing.Tokens.Concat(cf.Tokens);
                existing.Tokens = codeFileTokens.ToArray();
            }
            else
            {
                this.result.Add(resourceProvider, cf);
            }
        }

        public async Task GenerateCodeFile()
        {
            foreach (var (key, value) in this.result)
            {
                Console.WriteLine("Write generated codefile ${0}.swagger", key);
                // Rewrite codefile name with resource provider name
                value.Name = key;
                var fileWriteStream = File.OpenWrite($"{key}.swagger");
                await value.SerializeAsync(fileWriteStream);
                Console.WriteLine($"Generated output file {key}.swagger");
                
            }
        }
    }


    internal class Program
    {
        
        static Task<int> Main(string[] args)
        {   

            var swaggers = new Argument<IEnumerable<string> >("swaggers", "The input swaggers file. Can be a single file or multiple files separated by space.");
            
            var cmd = new RootCommand
            {
                swaggers,
            };

            cmd.Description = "Parse swagger file into codefile.";

            cmd.SetHandler(async (IEnumerable<string> swaggerFile) =>
            {
                await HandleGenerateCodeFile(swaggerFile);
            }, swaggers);

            return Task.FromResult(cmd.Invoke(args));

        }

        static async Task HandleGenerateCodeFile(IEnumerable<string> swaggers)
        {
            var swaggerFilePaths = swaggers as string[] ?? swaggers.ToArray();
            var parserResultProcessor = new ParserResultProcessor();
            foreach (var swaggerFilePath in swaggerFilePaths)
            {
                if (!File.Exists(swaggerFilePath))
                {
                    throw new FileNotFoundException(swaggerFilePath);
                }
                
                var input = Path.GetFullPath(swaggerFilePath);
                Console.WriteLine("Input swagger file: {0}", input);
                var output = input.Replace(".json", ".swagger");
                await using FileStream fileReadStream = File.OpenRead(input);
                SwaggerSpec swaggerSpec = await JsonSerializer.DeserializeAsync<SwaggerSpec>(fileReadStream);
                var resourceProvider = await GetResourceProviderFromSwagger(swaggerSpec);
                if (resourceProvider.Length == 0)
                {
                    throw new Exception($"Can not found resource provider from swagger file {swaggerFilePath}");
                }
                fileReadStream.Seek(0, SeekOrigin.Begin);

                var ls = new SwaggerLanguageService();
                var cf = await ls.GetCodeFileInternalAsync(input, fileReadStream, false);
                parserResultProcessor.AppendResult(resourceProvider, cf);
            }

            await parserResultProcessor.GenerateCodeFile();


        }

        private static async Task<string> GetResourceProviderFromSwagger(SwaggerSpec swaggerSpec)
        {
            List<string> paths = new List<string>(swaggerSpec.paths.Keys);
            foreach (var path in paths)
            {
                var resourceProvider = GetResourceProviderFromPath(path);
                if (resourceProvider != "")
                {
                    return resourceProvider;
                }
            }

            return "";
        }
        static string GetResourceProviderFromPath(string path)
        {
            const string resourceProviderPattern = "/providers/(:?[^{/]+)";
            var match = Regex.Match(path, resourceProviderPattern, RegexOptions.RightToLeft);
            return match.Success ? match.Groups[1].Value : "";
        }
    }
}
