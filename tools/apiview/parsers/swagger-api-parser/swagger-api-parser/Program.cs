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
            var input = args[0];
            var output = args[1];
            var fileReadStream = File.OpenRead(input);
            var ls = new SwaggerLanguageService();
            var cf = await ls.GetCodeFileInternalAsync(input, fileReadStream, false);
            var fileWriteStream = File.OpenWrite(output);
            await cf.SerializeAsync(fileWriteStream);
            Console.WriteLine("Hello World!");
        }
    }
}
