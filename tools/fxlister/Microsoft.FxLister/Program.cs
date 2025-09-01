using System.CommandLine;
using Microsoft.FxLister.Commands;

namespace Microsoft.FxLister;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("FxLister - A tool to analyze NuGet packages and extract type information");
        
        var typesCommand = TypesCommand.Create();
        rootCommand.AddCommand(typesCommand);
        
        return await rootCommand.InvokeAsync(args);
    }
}