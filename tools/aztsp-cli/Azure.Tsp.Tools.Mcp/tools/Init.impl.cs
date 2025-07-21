using System.Runtime.InteropServices;
using Azure.Tsp.Tools.Mcp.Helpers;
using Mcp;

namespace Azure.Tsp.Tools.Mcp.Tools;

public class InitImpl : IInit
{

    private static readonly string AZURE_TEMPLATES_URL = "https://aka.ms/typespec/azure-init";
    private static readonly string[] sourceArray = ["azure-core", "azure-arm"];

    private static string? ValidateOutputDirectory(string outputDir)
    {
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            return "Failed: outputDirectory must be provided";
        }

        var fullOutputDir = Path.GetFullPath(outputDir.Trim());
        if (string.IsNullOrEmpty(fullOutputDir))
        {
            return $"Failed: outputDirectory '{outputDir}' could not be resolved to a full path.";
        }

        if (!Directory.Exists(fullOutputDir))
        {
            return $"Failed: Full output directory '{fullOutputDir}' does not exist.";
        }

        if (Directory.GetFileSystemEntries(fullOutputDir).Length != 0)
        {
            return $"Failed: The full output directory '{fullOutputDir}' points to a non-empty directory.";
        }

        return null; // Validation passed
    }

    public Task<string> QuickstartAsync(string template, string serviceNamespace, string outputDirectory, CancellationToken cancellationToken)
    {
        try
        {
            // Validate template
            if (string.IsNullOrWhiteSpace(template) || !sourceArray.Contains(template.Trim()))
            {
                return Task.FromResult($"Failed: template must be one of: {string.Join(", ", sourceArray)} but was '{template}'");
            }
            // Validate serviceNamespace
            if (string.IsNullOrWhiteSpace(serviceNamespace))
            {
                return Task.FromResult($"Failed: serviceNamespace must be provided and cannot be empty.");
            }
            // Validate outputDir
            var validationResult = ValidateOutputDirectory(outputDirectory);
            if (validationResult != null)
            {
                return Task.FromResult(validationResult);
            }
            var fullOutputDir = Path.GetFullPath(outputDirectory.Trim());


            return Task.FromResult(RunTspInit(
              template: template,
              serviceNamespace: serviceNamespace,
              outputDir: fullOutputDir,
              cancellationToken: cancellationToken
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Failed: An error occurred trying to initialize TypeSpec project: {ex.Message}");
        }

    }


    public Task<string> ConvertSwaggerAsync(string pathToSwaggerReadme, string outputDirectory, bool? isAzureResourceManagement, bool? fullyCompatible, CancellationToken cancellationToken)
    {
        try
        {
            // Validate pathToSwaggerReadme
            if (string.IsNullOrWhiteSpace(pathToSwaggerReadme) || !pathToSwaggerReadme.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult($"Failed: pathToSwaggerReadme must be a valid Markdown file.");
            }
            var fullPathToSwaggerReadme = Path.GetFullPath(pathToSwaggerReadme.Trim());
            if (!new FileInfo(fullPathToSwaggerReadme).Exists)
            {
                return Task.FromResult($"Failed: pathToSwaggerReadme '{fullPathToSwaggerReadme}' does not exist.");
            }

            // validate outputDirectory using the extracted method
            var validationResult = ValidateOutputDirectory(outputDirectory);
            if (validationResult != null)
            {
                return Task.FromResult(validationResult);
            }
            var fullOutputDir = Path.GetFullPath(outputDirectory.Trim());

            return Task.FromResult(RunTspClient(
                pathToSwaggerReadme: fullPathToSwaggerReadme,
                outputDirectory: fullOutputDir,
                isAzureResourceManagement: isAzureResourceManagement ?? false,
                fullyCompatible: fullyCompatible ?? false
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Failed: An error occurred trying to convert '{pathToSwaggerReadme}': {ex.Message}");
        }
    }

    private static string RunTspInit(
      string template,
      string serviceNamespace,
      string outputDir,
      CancellationToken cancellationToken)
    {
        var argsList = new List<string>
        {
            "tsp",
            "init",
            "--template",
            template,
            "--project-name",
            serviceNamespace,
            "--args",
            $"ServiceNamespace={serviceNamespace}",
            "--output-dir",
            outputDir,
            "--no-prompt",
            AZURE_TEMPLATES_URL
        };

        var (Output, ExitCode) = ProcessHelper.RunNpx(argsList, Environment.CurrentDirectory);
        if (ExitCode != 0)
        {
            return $"Failed to initialize TypeSpec project: {Output}";
        }

        return $"TypeSpec project initialized successfully in {outputDir}";
    }

    private static string RunTspClient(
        string pathToSwaggerReadme,
        string outputDirectory,
        bool isAzureResourceManagement,
        bool fullyCompatible
    )
    {
        var argsList = new List<string>
        {
            "tsp-client",
            "convert",
            "--swagger-readme",
            pathToSwaggerReadme,
            "--output-dir",
            outputDirectory
        };

        if (isAzureResourceManagement == true)
        {
            argsList.Add("--arm");
        }

        if (fullyCompatible == true)
        {
            argsList.Add("--fully-compatible");
        }

        var (Output, ExitCode) = ProcessHelper.RunNpx(argsList, Environment.CurrentDirectory);

        if (ExitCode != 0)
        {
            return $"Failed to convert swagger to TypeSpec project: {Output}";
        }

        return $"Swagger successfully converted to TypeSpec project in {outputDirectory}";
    }
}
