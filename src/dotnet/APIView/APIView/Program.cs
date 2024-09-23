using System;
using System.IO;
using System.Threading.Tasks;

namespace ApiView
{
    public class Program
    {
        async static Task Main(string[] args)
        {
            try
            {
                await RunAsync(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public async static Task RunAsync(string[] args)
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("usage: ApiView [input-path] [output-path]", nameof(args));
            }

            var assemblySymbol = CompilationFactory.GetCompilation(args[0]);
            var codeNode = new CodeFileBuilder().Build(assemblySymbol, false, null);

            using var fileStream = new FileStream(args[1], FileMode.Create, FileAccess.Write);
            await codeNode.SerializeAsync(fileStream);
        }
    }
}
