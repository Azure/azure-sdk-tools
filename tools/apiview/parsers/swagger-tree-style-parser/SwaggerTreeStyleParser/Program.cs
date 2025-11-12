using System.CommandLine;
using ApiView;
using APIView;
using NSwag;

namespace SwaggerTreeStyleParser;

public class Program
{
    public const string CurrentVersion = "0.1";
    public static async Task<int> Main(string[] args)
    {
        var swaggers = new Option<IEnumerable<string>>(name: "--swaggers", aliases: "-s")
        {
            Description = "Input swagger file(s). Provide one or more paths separated by space.",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        var output = new Option<string>(name: "--output", aliases: "-o")
        {
            Description = "The output file path.",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => "swagger.json"
        };

        var readme = new Option<string>(name: "--readme", aliases: "-r")
        {
            Description = "The readme file path.",
            Arity = ArgumentArity.ExactlyOne
        };

        var tag = new Option<string>(name: "--tag", aliases: "-t")
        {
            Description = "Readme tag used to generate swagger apiView",
            Arity = ArgumentArity.ExactlyOne,
            DefaultValueFactory = _ => "default"
        };

        var package = new Option<string>(name: "--package-name", aliases: "-p")
        {
            Description = "The package name for the generated code file.",
            Arity = ArgumentArity.ExactlyOne,
            DefaultValueFactory = _ => "swagger"
        };

        var Links = new Option<IEnumerable<string>>(name: "--swagger-links", aliases: "-l")
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

        ParseResult parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                Console.WriteLine(error.Message);
            }
            return 1;
        }

        var swaggerFiles = parseResult.GetValue(swaggers);
        var outputFile = parseResult.GetValue(output);
        var readmeFile = parseResult.GetValue(readme);
        var readmeTag = parseResult.GetValue(tag);
        var packageName = parseResult.GetValue(package);
        var swaggerLinks = parseResult.GetValue(Links);

        await HandleGenerateCodeFile(swaggerFiles, outputFile, readmeFile, readmeTag, packageName, swaggerLinks);

        return 0;
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
