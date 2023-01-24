using System;
using System.IO;
using System.Threading.Tasks;

namespace ApiView
{
    public static class App
    {
        public async static Task RunAsync(string[] args)
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("usage: ApiView [input-path] [output-path]", nameof(args));
            }

            var assemblySymbol = CompilationFactory.GetCompilation(args[0]);
            var codeNode = new CodeFileBuilder().Build(assemblySymbol, false, null);

            using var fileStream = new FileStream(args[1], FileMode.OpenOrCreate, FileAccess.Write);
            await codeNode.SerializeAsync(fileStream);
        }
    }
}
