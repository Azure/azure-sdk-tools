using System.CommandLine;
using Microsoft.FxLister.Services;

namespace Microsoft.FxLister.Commands;

public static class TypesCommand
{
    public static Command Create()
    {
        var outputOption = new Option<string>(
            new[] { "-o", "--output" },
            "Output file path for the type list (without extension - will generate .txt and .qualified.txt)")
        {
            IsRequired = true
        };

        var maxPackagesOption = new Option<int>(
            new[] { "-m", "--max-packages" },
            () => 100,
            "Maximum number of packages to process");

        var packagePatternOption = new Option<string>(
            new[] { "-p", "--package-pattern" },
            () => @"^Azure\.(?!ResourceManager)(?!Provisioning)",
            "Regex pattern to filter package names");

        var command = new Command("types", "Extract type names from Azure NuGet packages")
        {
            outputOption,
            maxPackagesOption,
            packagePatternOption
        };

        command.SetHandler(async (outputPath, maxPackages, packagePattern) =>
        {
            try
            {
                // Use real implementations
                var packageAnalyzer = new PackageAnalyzer();
                var typeExtractor = new RealTypeExtractor();
                
                Console.WriteLine("Discovering Azure NuGet packages...");
                var packages = await packageAnalyzer.DiscoverAzurePackagesAsync(maxPackages, packagePattern);
                
                Console.WriteLine($"Found {packages.Count} Azure packages. Extracting types...");
                var typeInfos = await typeExtractor.ExtractTypesFromPackagesAsync(packages);
                
                // Sort by short name
                var sortedTypeInfos = typeInfos.OrderBy(t => t.ShortName).ToList();
                
                // Generate output file names
                var baseOutputPath = Path.GetFileNameWithoutExtension(outputPath);
                var outputDir = Path.GetDirectoryName(outputPath) ?? "";
                var shortNamesFile = Path.Combine(outputDir, $"{baseOutputPath}.txt");
                var qualifiedNamesFile = Path.Combine(outputDir, $"{baseOutputPath}.qualified.txt");
                
                // Write short names file (sorted)
                var shortNames = sortedTypeInfos.Select(t => t.ShortName);
                await File.WriteAllLinesAsync(shortNamesFile, shortNames);
                
                // Write qualified names file (same order as sorted short names)
                var qualifiedNames = sortedTypeInfos.Select(t => t.FullName);
                await File.WriteAllLinesAsync(qualifiedNamesFile, qualifiedNames);
                
                Console.WriteLine($"Successfully wrote {typeInfos.Count} type names to:");
                Console.WriteLine($"  Short names: {shortNamesFile}");
                Console.WriteLine($"  Qualified names: {qualifiedNamesFile}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, outputOption, maxPackagesOption, packagePatternOption);

        return command;
    }
}