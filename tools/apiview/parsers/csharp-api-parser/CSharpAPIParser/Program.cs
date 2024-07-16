using System.CommandLine;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using ApiView;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;


public static class Program
{
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
                    Console.Error.WriteLine($"PackageFile {packageFilePath.FullName} contains no dlls.");
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
                return;
            }

            var parsedFileName = string.IsNullOrEmpty(outputFileName) ? assemblySymbol.Name : outputFileName;
            var treeTokenCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, runAnalysis, dependencies);
            var gzipJsonTokenFilePath = Path.Combine(OutputDirectory.FullName, $"{parsedFileName}.json.tgz");


            var options = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            await using FileStream gzipFileStream = new FileStream(gzipJsonTokenFilePath, FileMode.Create, FileAccess.Write);
            await using GZipStream gZipStream = new GZipStream(gzipFileStream, CompressionLevel.Optimal);
            await JsonSerializer.SerializeAsync(gZipStream, treeTokenCodeFile, options);
            Console.WriteLine($"TokenCodeFile File {gzipJsonTokenFilePath} Generated Successfully.");
            Console.WriteLine();
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
