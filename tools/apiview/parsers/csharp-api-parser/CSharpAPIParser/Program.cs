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

                var dllEntry = dllEntries.First();
                if (dllEntries.Length > 1)
                {
                    // If there are multiple dlls in the nupkg (e.g. Cosmos), try to find the first that matches the nuspec name, but
                    // fallback to just using the first one.
                    dllEntry = dllEntries.FirstOrDefault(
                        dll => Path.GetFileNameWithoutExtension(nuspecEntry.Name)
                            .Equals(Path.GetFileNameWithoutExtension(dll.Name), StringComparison.OrdinalIgnoreCase)) ?? dllEntry;
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

            if (assemblySymbol == null)
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
                        var file = reader.GetFiles().FirstOrDefault(f => f.EndsWith(dep.Name + ".dll"));
                        if (file != null)
                        {
                            var fileInfo = new FileInfo(file);
                            var path = Path.Combine(tempFolder, dep.Name, fileInfo.Name);
                            var tmp = reader.ExtractFile(file, path, NullLogger.Instance);
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
