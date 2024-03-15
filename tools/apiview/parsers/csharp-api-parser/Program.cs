using System.CommandLine;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using ApiView;

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

rootCommand.SetHandler((FileInfo packageFilePath, DirectoryInfo outputDirectory, string outputFileName, bool runAnalysis) =>
{
    try
    {
        using (var stream = packageFilePath.OpenRead())
        {
            HandlePackageFileParsing(stream, packageFilePath, outputDirectory, outputFileName, runAnalysis);
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error Reading PackageFile : {ex.Message}");
    }
}, inputOption, outputOption1,outputOption2, runAnalysis);

return rootCommand.InvokeAsync(args).Result;


static void HandlePackageFileParsing(Stream stream, FileInfo packageFilePath, DirectoryInfo OutputDirectory, string outputFileName, bool runAnalysis)
{
    ZipArchive? zipArchive = null;
    Stream? dllStream = stream;
    Stream? docStream = null;
    List<DependencyInfo>? dependencies = null;

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
            dependencies = new List<DependencyInfo>();
            dependencies.AddRange(
                    dependencyElements.Select(dependency => new DependencyInfo(
                            dependency.Attribute("id")?.Value,
                                dependency.Attribute("version")?.Value)));
            // filter duplicates and sort
            if (dependencies.Any())
            {
                dependencies = dependencies
                .GroupBy(d => d.Name)
                .Select(d => d.First())
                .OrderBy(d => d.Name).ToList();
            }
        }

        var assemblySymbol = CompilationFactory.GetCompilation(dllStream, docStream);
        if (assemblySymbol == null)
        {
            Console.Error.WriteLine($"PackageFile {packageFilePath.FullName} contains no Assembly Symbol.");
            return;
        }
        var parsedFileName = string.IsNullOrEmpty(outputFileName) ? assemblySymbol.Name : outputFileName;
        var treeTokenCodeFile = new CSharpAPIParser.TreeToken.CodeFileBuilder().Build(assemblySymbol, runAnalysis, dependencies);
        var gzipJsonTokenFilePath = Path.Combine(OutputDirectory.FullName, $"{parsedFileName}");


        var options = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        {
            using FileStream gzipFileStream = new FileStream(gzipJsonTokenFilePath, FileMode.Create, FileAccess.Write);
            using GZipStream gZipStream = new GZipStream(gzipFileStream, CompressionLevel.Optimal);
            JsonSerializer.Serialize(new Utf8JsonWriter(gZipStream, new JsonWriterOptions { Indented = false }), treeTokenCodeFile, options);
        }

        Console.WriteLine($"TokenCodeFile File {gzipJsonTokenFilePath} Generated Successfully.");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error Parsing PackageFile : {ex.Message}");
    }
    finally
    {
        zipArchive?.Dispose();
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
