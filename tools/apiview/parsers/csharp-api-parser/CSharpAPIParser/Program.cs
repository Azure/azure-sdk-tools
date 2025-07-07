using System.CommandLine;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ApiView;
using APIView.Model.V2;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Frameworks;


public static class Program
{
    // Regex parser nuget file name without extension to two groups
    // package name and package version
    // for e.g Azure.Core.1.0.0 to ["Azure.Core", "1.0.0"]
    // or Azure.Storage.Blobs.12.0.0 to ["Azure.Storage.Blobs", "12.0.0"]
    private static Regex _packageNameParser = new Regex("([A-Za-z.]*[a-z]).([\\S]*)", RegexOptions.Compiled);

    public static int Main(string[] args)
    {
        var inputOption = new Option<FileInfo>("--packageFilePath", "C# Package (.nupkg) file").ExistingOnly();
        inputOption.IsRequired = true;

        var outputOption1 = new Option<DirectoryInfo>("--outputDirectoryPath", "Directory for the output Token File").ExistingOnly();
        var outputOption2 = new Option<string>("--outputFileName", "Output File Name");
        var runAnalysis = new Argument<bool>("runAnalysis", "Run Analysis on the package");
        runAnalysis.SetDefaultValue(true);

        var rootCommand = new RootCommand("Parse C# Package (.nupkg) to APIView Tokens")
        {
            inputOption,
            outputOption1,
            outputOption2,
            runAnalysis
        };

        rootCommand.SetHandler(async (FileInfo packageFilePath, DirectoryInfo outputDirectory, string outputFileName, bool runAnalysis) =>
        {
            try
            {
                using (var stream = packageFilePath.OpenRead())
                {
                    await HandlePackageFileParsing(stream, packageFilePath, outputDirectory, outputFileName, runAnalysis);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error Reading PackageFile : {ex.Message}");
                throw;
            }
        }, inputOption, outputOption1, outputOption2, runAnalysis);

        return rootCommand.InvokeAsync(args).Result;
    }


    static async Task HandlePackageFileParsing(Stream stream, FileInfo packageFilePath, DirectoryInfo OutputDirectory, string outputFileName, bool runAnalysis)
    {
        ZipArchive? zipArchive = null;
        Stream? dllStream = stream;
        Stream? docStream = null;
        List<DependencyInfo>? dependencies = new List<DependencyInfo>();
        string? dependencyFilesTempDir = null;

        try
        {
            if (IsNuget(packageFilePath.FullName))
            {
                zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                var nuspecEntry = zipArchive.Entries.Single(entry => IsNuspec(entry.Name));
                var dllEntries = zipArchive.Entries.Where(entry => IsDll(entry.Name)).ToArray();

                if (dllEntries.Length == 0)
                {
                    Console.Error.WriteLine($"PackageFile {packageFilePath.FullName} contains no dll. Creating a meta package API review file.");
                    var codeFile = CreateDummyCodeFile(packageFilePath.FullName, $"Package {packageFilePath.Name} does not contain any dll to create API review.");
                    outputFileName = string.IsNullOrEmpty(outputFileName) ? nuspecEntry.Name : outputFileName;
                    await CreateOutputFile(OutputDirectory.FullName, outputFileName, codeFile);
                    return;
                }

                var dllEntry = SelectBestDllEntry(dllEntries, Path.GetFileNameWithoutExtension(nuspecEntry.Name));
                if (dllEntry == null)
                {
                    Console.Error.WriteLine($"PackageFile {packageFilePath.FullName} contains no suitable dll. Creating a meta package API review file.");
                    var codeFile = CreateDummyCodeFile(packageFilePath.FullName, $"Package {packageFilePath.Name} does not contain any suitable dll to create API review.");
                    outputFileName = string.IsNullOrEmpty(outputFileName) ? nuspecEntry.Name : outputFileName;
                    await CreateOutputFile(OutputDirectory.FullName, outputFileName, codeFile);
                    return;
                }

                dllStream = dllEntry.Open();
                var docEntry = zipArchive.GetEntry(Path.ChangeExtension(dllEntry.FullName, ".xml"));
                if (docEntry != null)
                {
                    docStream = docEntry.Open();
                }
                using var nuspecStream = nuspecEntry.Open();
                var document = XDocument.Load(nuspecStream);
                var dependencyElements = document.Descendants().Where(e => e.Name.LocalName == "dependency");
                dependencies.AddRange(
                        dependencyElements.Select(dependency => new DependencyInfo(
                                dependency.Attribute("id")?.Value,
                                    SelectSpecificVersion(dependency.Attribute("version")?.Value))));
                // filter duplicates and sort
                if (dependencies.Any())
                {
                    dependencies = dependencies
                    .GroupBy(d => d.Name)
                    .Select(d => d.First())
                    .OrderBy(d => d.Name).ToList();
                }
            }

            IEnumerable<string> dependencyFilePaths = new List<string>();
            if (dependencies != null && dependencies.Any())
            {
                dependencyFilesTempDir = await ExtractNugetDependencies(dependencies).ConfigureAwait(false);
                if (Directory.Exists(dependencyFilesTempDir))
                {
                    dependencyFilePaths = Directory.EnumerateFiles(dependencyFilesTempDir, "*.dll", SearchOption.AllDirectories);
                }
            }
            var assemblySymbol = CompilationFactory.GetCompilation(dllStream, docStream, dependencyFilePaths);

            if (assemblySymbol == null || string.IsNullOrEmpty(assemblySymbol.Name))
            {
                Console.Error.WriteLine($"PackageFile {packageFilePath.FullName} contains no Assembly Symbol.");
                var codeFile = CreateDummyCodeFile(packageFilePath.FullName, $"Package {packageFilePath.Name} does not contain any assembly symbol to create API review.");
                outputFileName = string.IsNullOrEmpty(outputFileName) ? packageFilePath.Name : outputFileName;
                await CreateOutputFile(OutputDirectory.FullName, outputFileName, codeFile);
                return;
            }

            var parsedFileName = string.IsNullOrEmpty(outputFileName) ? assemblySymbol.Name : outputFileName;
            var treeTokenCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, runAnalysis, dependencies);
            await CreateOutputFile(OutputDirectory.FullName, parsedFileName, treeTokenCodeFile);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error Parsing PackageFile : {ex.Message}");
            throw;
        }
        finally
        {
            zipArchive?.Dispose();
            if (dependencyFilesTempDir != null && Directory.Exists(dependencyFilesTempDir))
            {
                Directory.Delete(dependencyFilesTempDir, true);
            }
        }
    }

    static async Task CreateOutputFile(string outputPath, string outputFileNamePrefix, CodeFile apiViewFile)
    {
        var jsonTokenFilePath = Path.Combine(outputPath, $"{outputFileNamePrefix}.json");
        await using FileStream fileStream = new(jsonTokenFilePath, FileMode.Create, FileAccess.Write);
        await apiViewFile.SerializeAsync(fileStream);
        Console.WriteLine($"TokenCodeFile File {jsonTokenFilePath} Generated Successfully.");
        Console.WriteLine();
    }

    /*** Creates dummy API review file to support meta packages.*/
    static CodeFile CreateDummyCodeFile(string originalName, string text)
    {
        var packageName = Path.GetFileNameWithoutExtension(originalName);
        var packageNameMatch = _packageNameParser.Match(packageName);
        var packageVersion = "";
        if (packageNameMatch.Success)
        {
            packageName = packageNameMatch.Groups[1].Value;
            packageVersion = $"{packageNameMatch.Groups[2].Value}";
        }

        var codeFile = new CodeFile();
        codeFile.PackageName = packageName;
        codeFile.PackageVersion = packageVersion;
        codeFile.ReviewLines.Add(new ReviewLine
        {
            Tokens = new List<ReviewToken>
            {
                ReviewToken.CreateTextToken(text)
            }
        });
        return codeFile;
    }

    static bool IsNuget(string name)
    {
        return name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsNuspec(string name)
    {
        return name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsDll(string name)
    {
        return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the target framework from a DLL path within a NuGet package.
    /// </summary>
    /// <param name="path">The path to the DLL within the package (e.g., "lib/net8.0/System.Text.Json.dll")</param>
    /// <returns>The target framework moniker or null if not found</returns>
    static string? ParseTargetFrameworkFromPath(string path)
    {
        // Expected format: lib/{tfm}/{assembly}.dll or ref/{tfm}/{assembly}.dll
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && (parts[0] == "lib" || parts[0] == "ref"))
        {
            return parts[1];
        }
        return null;
    }

    /// <summary>
    /// Selects the best target framework from the available options.
    /// Prefers newer and more specific frameworks.
    /// </summary>
    /// <param name="availableFrameworks">Available target framework monikers</param>
    /// <returns>The best target framework moniker</returns>
    static string? SelectBestTargetFramework(IEnumerable<string> availableFrameworks)
    {
        if (!availableFrameworks.Any()) return null;

        var frameworks = availableFrameworks.Distinct().ToList();
        
        // Define framework preference order (higher number = better)
        var frameworkPreferences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "net462", 1 },
            { "net47", 2 },
            { "net471", 3 },
            { "net472", 4 },
            { "net48", 5 },
            { "netstandard1.0", 10 },
            { "netstandard1.1", 11 },
            { "netstandard1.2", 12 },
            { "netstandard1.3", 13 },
            { "netstandard1.4", 14 },
            { "netstandard1.5", 15 },
            { "netstandard1.6", 16 },
            { "netstandard2.0", 20 },
            { "netstandard2.1", 25 },
            { "netcoreapp1.0", 30 },
            { "netcoreapp1.1", 31 },
            { "netcoreapp2.0", 32 },
            { "netcoreapp2.1", 33 },
            { "netcoreapp2.2", 34 },
            { "netcoreapp3.0", 35 },
            { "netcoreapp3.1", 36 },
            { "net5.0", 50 },
            { "net6.0", 60 },
            { "net7.0", 70 },
            { "net8.0", 80 },
            { "net9.0", 90 }
        };

        // Find the framework with the highest preference score
        var bestFramework = frameworks
            .Select(tfm => new { Framework = tfm, Score = frameworkPreferences.GetValueOrDefault(tfm, 0) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Framework) // For consistent ordering when scores are equal
            .FirstOrDefault();

        return bestFramework?.Framework;
    }

    /// <summary>
    /// Selects the best DLL entry from a NuGet package based on target framework preferences.
    /// </summary>
    /// <param name="dllEntries">All DLL entries in the package</param>
    /// <param name="packageName">The package name for fallback matching</param>
    /// <returns>The best DLL entry or null if none suitable found</returns>
    static ZipArchiveEntry? SelectBestDllEntry(ZipArchiveEntry[] dllEntries, string packageName)
    {
        if (dllEntries.Length == 0) return null;
        if (dllEntries.Length == 1) return dllEntries[0];

        // Group DLLs by target framework
        var dllsByFramework = dllEntries
            .Select(dll => new { Entry = dll, Framework = ParseTargetFrameworkFromPath(dll.FullName) })
            .Where(x => x.Framework != null)
            .GroupBy(x => x.Framework)
            .ToDictionary(g => g.Key!, g => g.ToList());

        if (dllsByFramework.Count == 0)
        {
            // Fallback to original logic if no framework structure found
            return dllEntries.FirstOrDefault(dll => 
                Path.GetFileNameWithoutExtension(packageName)
                    .Equals(Path.GetFileNameWithoutExtension(dll.Name), StringComparison.OrdinalIgnoreCase)) 
                ?? dllEntries.First();
        }

        // Select the best target framework
        var bestFramework = SelectBestTargetFramework(dllsByFramework.Keys);
        if (bestFramework == null) return dllEntries.First();

        // Get DLLs for the best framework
        var bestFrameworkDlls = dllsByFramework[bestFramework];
        
        // If multiple DLLs for the same framework, prefer the one matching the package name
        if (bestFrameworkDlls.Count > 1)
        {
            var matchingDll = bestFrameworkDlls.FirstOrDefault(dll =>
                Path.GetFileNameWithoutExtension(packageName)
                    .Equals(Path.GetFileNameWithoutExtension(dll.Entry.Name), StringComparison.OrdinalIgnoreCase));
            
            if (matchingDll != null) return matchingDll.Entry;
        }

        return bestFrameworkDlls.First().Entry;
    }

    /// <summary>
    /// Resolves the NuGet package dependencies and extracts them to a temporary folder. It is the responsibility of teh caller to clean up the folder.
    /// </summary>
    /// <param name="dependencyInfos">The dependency infos</param>
    /// <returns>A temporary path where the dependency files were extracted.</returns>
    public static async Task<string> ExtractNugetDependencies(List<DependencyInfo> dependencyInfos)
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        SourceCacheContext cache = new SourceCacheContext();
        SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        try
        {
            FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>().ConfigureAwait(false);
            foreach (var dep in dependencyInfos)
            {
                using (MemoryStream packageStream = new MemoryStream())
                {
                    if (await resource.CopyNupkgToStreamAsync(
                    dep.Name,
                    new NuGetVersion(dep.Version),
                    packageStream,
                    cache,
                    NullLogger.Instance,
                    CancellationToken.None))
                    {
                        using PackageArchiveReader reader = new PackageArchiveReader(packageStream);
                        NuspecReader nuspec = reader.NuspecReader;
                        var allFiles = reader.GetFiles().Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToArray();
                        
                        // Group files by target framework and select the best one
                        var filesByFramework = allFiles
                            .Select(file => new { File = file, Framework = ParseTargetFrameworkFromPath(file) })
                            .Where(x => x.Framework != null)
                            .GroupBy(x => x.Framework)
                            .ToDictionary(g => g.Key!, g => g.ToList());

                        string? selectedFile = null;
                        if (filesByFramework.Count > 0)
                        {
                            var bestFramework = SelectBestTargetFramework(filesByFramework.Keys);
                            if (bestFramework != null)
                            {
                                var bestFrameworkFiles = filesByFramework[bestFramework];
                                // Prefer file that matches dependency name
                                selectedFile = bestFrameworkFiles.FirstOrDefault(f => 
                                    Path.GetFileNameWithoutExtension(f.File).Equals(dep.Name, StringComparison.OrdinalIgnoreCase))?.File
                                    ?? bestFrameworkFiles.First().File;
                            }
                        }
                        else
                        {
                            // Fallback to original logic if no framework structure
                            selectedFile = allFiles.FirstOrDefault(f => f.EndsWith(dep.Name + ".dll"));
                        }

                        if (selectedFile != null)
                        {
                            var fileInfo = new FileInfo(selectedFile);
                            var path = Path.Combine(tempFolder, dep.Name, fileInfo.Name);
                            var tmp = reader.ExtractFile(selectedFile, path, NullLogger.Instance);
                        }
                    }
                }
            }
        }
        finally
        {
            cache.Dispose();
        }
        return tempFolder;
    }

    public static string? SelectSpecificVersion(string? versionRange)
    {
        if (string.IsNullOrEmpty(versionRange))
        {
            return null;
        }
        var range = VersionRange.Parse(versionRange);
        if (range.HasUpperBound)
        {
            var maxVersion = range.MaxVersion;
            if (maxVersion != null)
            {
                if (range.IsMaxInclusive)
                {
                    return maxVersion.ToString();
                }
                else
                {
                    return maxVersion.Version.ToString();
                }
            }
        }
        var specificVersion = range.MinVersion;
        return specificVersion?.ToString();
    }
}
