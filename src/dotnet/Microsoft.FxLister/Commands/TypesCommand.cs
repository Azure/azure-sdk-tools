using System.CommandLine;
using Microsoft.FxLister.Services;

namespace Microsoft.FxLister.Commands;

public static class TypesCommand
{
    public static Command Create()
    {
        var outputOption = new Option<string>(
            new[] { "-o", "--output" },
            "Output file path for the type list")
        {
            IsRequired = true
        };

        var maxPackagesOption = new Option<int>(
            new[] { "-m", "--max-packages" },
            () => 100,
            "Maximum number of packages to process");

        var command = new Command("types", "Extract type names from Azure NuGet packages")
        {
            outputOption,
            maxPackagesOption
        };

        command.SetHandler(async (outputPath, maxPackages) =>
        {
            try
            {
                // Use real implementations
                var packageAnalyzer = new PackageAnalyzer();
                var typeExtractor = new RealTypeExtractor();
                
                Console.WriteLine("Discovering Azure NuGet packages...");
                var packages = await packageAnalyzer.DiscoverAzurePackagesAsync(maxPackages);
                
                Console.WriteLine($"Found {packages.Count} Azure packages. Extracting types...");
                var types = await typeExtractor.ExtractTypesFromPackagesAsync(packages);
                
                Console.WriteLine($"Found {types.Count} types. Writing to {outputPath}...");
                await File.WriteAllLinesAsync(outputPath, types.OrderBy(t => t));
                
                Console.WriteLine($"Successfully wrote {types.Count} type names to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, outputOption, maxPackagesOption);

        return command;
    }
}