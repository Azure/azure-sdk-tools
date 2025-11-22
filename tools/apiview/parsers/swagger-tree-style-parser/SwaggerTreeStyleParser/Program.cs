using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;

using ApiView;
using NSwag;

namespace SwaggerTreeStyleParser;

public class Program
{
    public const string CurrentVersion = "0.1";
    public static int Main(string[] args)
    {
        var swaggers = new Option<IEnumerable<string>>(aliases: new string[] { "--swaggers", "-s" })
        {
            Description = "Input swagger file(s). Provide one or more paths separated by space.",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        var output = new Option<string>(aliases: new string[] { "--output", "-o" }, getDefaultValue: () => "swagger.json")
        {
            Description = "The output file path.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var readme = new Option<string>(aliases: new string[] { "--readme", "-r" })
        {
            Description = "The readme file path.",
            Arity = ArgumentArity.ExactlyOne
        };

        var tag = new Option<string>(aliases: new string[] { "--tag", "-t" }, getDefaultValue: () => "default")
        {
            Description = "Readme tag used to generate swagger apiView",
            Arity = ArgumentArity.ExactlyOne
        };

        var package = new Option<string>(aliases: new string[] { "--package-name", "-p" }, getDefaultValue: () => "swagger")
        {
            Description = "The package name for the generated code file.",
            Arity = ArgumentArity.ExactlyOne
        };

        var Links = new Option<IEnumerable<string>>(aliases: new string[] { "--swagger-links", "-l" })
        {
            Description = "Input swagger file links. Provide one or more URLs separated by space.",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        var rootCommand = new RootCommand(description: "Parse swagger file into codefile.")
        {
            swaggers,
            output,
            readme,
            tag,
            package,
            Links
        };

        rootCommand.SetHandler(async (IEnumerable<string>? swaggerFiles, string? outputFile, string? readmeFile, string? readmeTag, string? packageName, IEnumerable<string>? swaggerLinks) =>
        {
            await HandleGenerateCodeFile(swaggerFiles, outputFile, readmeFile, readmeTag, packageName, swaggerLinks);
        }, swaggers, output, readme, tag, package, Links);

        return rootCommand.InvokeAsync(args).Result;
    }

    static async Task HandleGenerateCodeFile(IEnumerable<string>? swaggerFiles, string? outputFile, string? readmeFile, string? readmeTag, string? packageName, IEnumerable<string>? swaggerLinks)
    {
        var swaggerFilePaths = swaggerFiles?.ToList();
        if (readmeFile != null && readmeTag != null)
        {
            var readmeFileDir = Path.GetDirectoryName(readmeFile);
            var swaggerfiles = ReadmeParser.GetSwaggerFilesFromReadme(readmeFile, readmeTag);
            swaggerFilePaths = swaggerFilePaths?.Concat(swaggerfiles.Select(it => Path.Join(readmeFileDir, it))).ToList();
        }

        if (swaggerFilePaths == null || !swaggerFilePaths.Any())
        {
            Console.WriteLine("No swagger files specified. For usage, run with --help");
            return;
        }

        var codeFile = new CodeFile()
        {
            Language = "Swagger",
            ParserVersion = CurrentVersion,
            PackageName = packageName,
            Name = packageName,
        };

        foreach (var swaggerFilePath in swaggerFilePaths)
        {
            if (!File.Exists(swaggerFilePath)) 
            {
                Console.WriteLine($"Invalid file path to swagger file. Skipping {swaggerFilePath}.");
                continue;
            }

            Console.WriteLine(swaggerFilePath);
            OpenApiDocument openApiDocument = await OpenApiDocument.FromFileAsync(swaggerFilePath);
            codeFile.ParserVersion = openApiDocument.Info.Version;
            var reviewLines = new CodeFileBuilder().Build(openApiDocument!);
            codeFile.ReviewLines.AddRange(reviewLines);
        }

        var outputFilePath = Path.GetFullPath(outputFile!);
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        Console.WriteLine($"Generate codefile {outputFile} successfully.");
        await codeFile.SerializeAsync(writer);

        return;
    }
}
