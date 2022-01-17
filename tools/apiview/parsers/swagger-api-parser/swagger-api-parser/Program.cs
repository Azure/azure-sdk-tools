using System;
using System.IO;
using System.Threading.Tasks;
using System.CommandLine;
using APIViewWeb;

namespace swagger_api_parser
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {   

            var swagger = new Argument<string>("swagger", "The input swagger file.");

            var cmd = new RootCommand
            {
                swagger,
            };

            cmd.Description = "Parse swagger file into codefile.";

            cmd.SetHandler(async (string swagger) =>
            {
                await handleGenerateCodeFile(swagger);
            }, swagger);

            return cmd.Invoke(args);

        }

        static async Task handleGenerateCodeFile(string swagger)
        {
            var input = Path.GetFullPath(swagger);

            Console.WriteLine("Input swagger file: {0}", input);
            var output = input.Replace(".json", ".swagger");
            var fileReadStream = File.OpenRead(input);
            var ls = new SwaggerLanguageService();
            var cf = await ls.GetCodeFileInternalAsync(input, fileReadStream, false);
            var fileWriteStream = File.OpenWrite(output);
            await cf.SerializeAsync(fileWriteStream);
            Console.WriteLine("Generated output file {0}", output);

        }
    }
}
