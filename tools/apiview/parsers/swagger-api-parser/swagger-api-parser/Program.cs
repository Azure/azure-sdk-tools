using System;
using System.IO;
using System.Threading.Tasks;
using APIViewWeb;

namespace swagger_api_parser
{
    internal class Program
    {
        static async Task Main(string[] args)
        {   
            if(args.Length == 0)
            {
                throw new ArgumentException("swagger");
            }
            var input = Path.GetFullPath(args[0]);

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
