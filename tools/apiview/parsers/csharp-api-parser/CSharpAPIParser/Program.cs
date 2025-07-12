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
                var dllEntries = new List<ZipArchiveEntry>();
                foreach (var entry in zipArchive.Entries)
                {
                    if (IsDll(entry.Name))
                    {
                        dllEntries.Add(entry);
                    }
                }

                if (dllEntries.Count == 0)
                {
                    Console.Error.WriteLine($"PackageFile {packageFilePath.FullName} contains no dll. Creating a meta package API review file.");
                    var codeFile = CreateDummyCodeFile(packageFilePath.FullName, $"Package {packageFilePath.Name} does not contain any dll to create API review.");
                    outputFileName = string.IsNullOrEmpty(outputFileName) ? nuspecEntry.Name : outputFileName;
                    await CreateOutputFile(OutputDirectory.FullName, outputFileName, codeFile);
                    return;
                }

                var dllEntry = SelectBestDllEntry(dllEntries.ToArray(), Path.GetFileNameWithoutExtension(nuspecEntry.Name));
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
                var dependencyElements = new List<XElement>();
                foreach (var element in document.Descendants())
                {
                    if (element.Name.LocalName == "dependency")
                    {
                        dependencyElements.Add(element);
                    }
                }
                
                foreach (var dependency in dependencyElements)
                {
                    dependencies.Add(new DependencyInfo(
                        dependency.Attribute("id")?.Value,
                        SelectSpecificVersion(dependency.Attribute("version")?.Value)));
                }
                
                // filter duplicates and sort
                if (dependencies.Count > 0)
                {
                    var uniqueDependencies = new Dictionary<string, DependencyInfo>(StringComparer.OrdinalIgnoreCase);
                    foreach (var dep in dependencies)
                    {
                        if (dep.Name != null && !uniqueDependencies.ContainsKey(dep.Name))
                        {
                            uniqueDependencies[dep.Name] = dep;
                        }
                    }
                    dependencies.Clear();
                    foreach (var kvp in uniqueDependencies)
                    {
                        dependencies.Add(kvp.Value);
                    }
                    // Simple sort by name
                    dependencies.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
                }
            }

            IEnumerable<string> dependencyFilePaths = new List<string>();
            if (dependencies != null && dependencies.Count > 0)
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
    public static string? ParseTargetFrameworkFromPath(string path)
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
    /// Prefers netstandard2.0 for compatibility, then other frameworks.
    /// </summary>
    /// <param name="availableFrameworks">Available target framework monikers</param>
    /// <returns>The best target framework moniker</returns>
    public static string? SelectBestTargetFramework(IEnumerable<string> availableFrameworks)
    {
        // Define framework preference order (most preferred first)
        var preferredFrameworks = new[]
        {
            "netstandard2.0", // Most preferred for compatibility
            "net8.0",
            "net7.0",
            "net6.0",
            "net5.0",
            "net48",
            "net472",
            "net471",
            "net47",
            "net462",
            "netcoreapp3.1",
            "netcoreapp3.0",
            "netcoreapp2.2",
            "netcoreapp2.1",
            "netcoreapp2.0",
            "netcoreapp1.1",
            "netcoreapp1.0",
            "netstandard2.1",
            "netstandard1.6",
            "netstandard1.5",
            "netstandard1.4",
            "netstandard1.3",
            "netstandard1.2",
            "netstandard1.1",
            "netstandard1.0"
        };

        // Create a HashSet for faster lookup
        var availableSet = new HashSet<string>(availableFrameworks, StringComparer.OrdinalIgnoreCase);
        
        if (availableSet.Count == 0) return null;

        // Find the first preferred framework that exists in available frameworks
        foreach (var preferred in preferredFrameworks)
        {
            if (availableSet.Contains(preferred))
            {
                return preferred;
            }
        }

        // If no preferred framework found, return the first available one
        foreach (var available in availableFrameworks)
        {
            return available;
        }

        return null;
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
        var dllsByFramework = new Dictionary<string, List<ZipArchiveEntry>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var dll in dllEntries)
        {
            var framework = ParseTargetFrameworkFromPath(dll.FullName);
            if (framework != null)
            {
                if (!dllsByFramework.ContainsKey(framework))
                {
                    dllsByFramework[framework] = new List<ZipArchiveEntry>();
                }
                dllsByFramework[framework].Add(dll);
            }
        }

        if (dllsByFramework.Count == 0)
        {
            // Fallback to original logic if no framework structure found
            var packageBaseName = Path.GetFileNameWithoutExtension(packageName);
            foreach (var dll in dllEntries)
            {
                if (packageBaseName.Equals(Path.GetFileNameWithoutExtension(dll.Name), StringComparison.OrdinalIgnoreCase))
                {
                    return dll;
                }
            }
            return dllEntries[0];
        }

        // Select the best target framework
        var bestFramework = SelectBestTargetFramework(dllsByFramework.Keys);
        if (bestFramework == null) return dllEntries[0];

        // Get DLLs for the best framework
        var bestFrameworkDlls = dllsByFramework[bestFramework];
        
        // If multiple DLLs for the same framework, prefer the one matching the package name
        if (bestFrameworkDlls.Count > 1)
        {
            var packageBaseName = Path.GetFileNameWithoutExtension(packageName);
            foreach (var dll in bestFrameworkDlls)
            {
                if (packageBaseName.Equals(Path.GetFileNameWithoutExtension(dll.Name), StringComparison.OrdinalIgnoreCase))
                {
                    return dll;
                }
            }
        }

        return bestFrameworkDlls[0];
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
                        var allFiles = new List<string>();
                        foreach (var file in reader.GetFiles())
                        {
                            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                            {
                                allFiles.Add(file);
                            }
                        }
                        
                        // Group files by target framework and select the best one
                        var filesByFramework = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                        foreach (var file in allFiles)
                        {
                            var framework = ParseTargetFrameworkFromPath(file);
                            if (framework != null)
                            {
                                if (!filesByFramework.ContainsKey(framework))
                                {
                                    filesByFramework[framework] = new List<string>();
                                }
                                filesByFramework[framework].Add(file);
                            }
                        }

                        string? selectedFile = null;
                        if (filesByFramework.Count > 0)
                        {
                            var bestFramework = SelectBestTargetFramework(filesByFramework.Keys);
                            if (bestFramework != null)
                            {
                                var bestFrameworkFiles = filesByFramework[bestFramework];
                                // Prefer file that matches dependency name
                                foreach (var file in bestFrameworkFiles)
                                {
                                    if (Path.GetFileNameWithoutExtension(file).Equals(dep.Name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        selectedFile = file;
                                        break;
                                    }
                                }
                                if (selectedFile == null && bestFrameworkFiles.Count > 0)
                                {
                                    selectedFile = bestFrameworkFiles[0];
                                }
                            }
                        }
                        else
                        {
                            // Fallback to original logic if no framework structure
                            foreach (var file in allFiles)
                            {
                                if (file.EndsWith(dep.Name + ".dll"))
                                {
                                    selectedFile = file;
                                    break;
                                }
                            }
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
