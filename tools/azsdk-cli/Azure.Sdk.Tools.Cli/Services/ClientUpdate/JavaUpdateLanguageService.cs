// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// Java-specific update language service.
/// </summary>
public class JavaUpdateLanguageService : ClientUpdateLanguageServiceBase
{
    private readonly ILogger<JavaUpdateLanguageService> _logger;

    public JavaUpdateLanguageService(ILanguageSpecificCheckResolver languageSpecificCheckResolver, ILogger<JavaUpdateLanguageService> logger) : base(languageSpecificCheckResolver)
    {
        _logger = logger;
    }

    public override string SupportedLanguage => "java";

    private const string CustomizationDirName = "customization";

    public override async Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
    {
        if (string.IsNullOrWhiteSpace(oldGenerationPath) || string.IsNullOrWhiteSpace(newGenerationPath))
        {
            throw new InvalidOperationException("Java API diff requires both oldGenerationPath and newGenerationPath.");
        }
        // Always use APIView methodIndex diff; treat empty list as no-change.
        return await RunApiViewDiffAsync(oldGenerationPath!, newGenerationPath!, CancellationToken.None);
    }

    /// <summary>
    /// Run the APIView Java processor on both generations, parse methodIndex maps, and compute a diff.
    /// Returns empty list if processor cannot build or methodIndex missing.
    /// </summary>
    private async Task<List<ApiChange>> RunApiViewDiffAsync(string oldPath, string newPath, CancellationToken ct)
    {
        var changes = new List<ApiChange>();
        try
        {
            // TODO: This might not be required
            var processorJar = await JavaApiViewJarBuilder.BuildProcessorJarAsync(_logger, ct);
            if (processorJar == null)
            {
                return changes;
            }

            // Collect input sources for each generation root.
            var oldInputs = DiscoverJavaInputs(oldPath);
            var newInputs = DiscoverJavaInputs(newPath);
            if (oldInputs.Count == 0 || newInputs.Count == 0)
            {
                _logger.LogDebug("APIView diff: no inputs discovered (old={OldCount}, new={NewCount})", oldInputs.Count, newInputs.Count);
                return changes;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "apiview-diff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var oldOut = Path.Combine(tempRoot, "old");
            var newOut = Path.Combine(tempRoot, "new");
            Directory.CreateDirectory(oldOut);
            Directory.CreateDirectory(newOut);

            // Run processor for each side.
            if (!await RunAPIViewProcessorAsync(processorJar, oldInputs, oldOut, ct) || !await RunAPIViewProcessorAsync(processorJar, newInputs, newOut, ct))
            {
                return new List<ApiChange>();
            }

            var oldIndex = JavaApiViewMethodIndexLoader.LoadMerged(oldOut, _logger);
            var newIndex = JavaApiViewMethodIndexLoader.LoadMerged(newOut, _logger);
            if (oldIndex.Count == 0 && newIndex.Count == 0)
            {
                return changes;
            }
            return JavaApiViewMethodIndexLoader.ComputeChanges(oldIndex, newIndex);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "APIView diff path failed");
            return new List<ApiChange>();
        }
    }
    private List<string> DiscoverJavaInputs(string generationRoot)
    {
        var inputs = new List<string>();
        try
        {
            if (Directory.Exists(generationRoot))
            {
                if (Directory.EnumerateFiles(generationRoot, "*.java", SearchOption.AllDirectories).Any())
                {
                    inputs.Add(generationRoot);
                }
                else
                {
                    _logger.LogDebug("No .java files found under provided generation root {Root}", generationRoot);
                }
            }
            else
            {
                _logger.LogDebug("Generation root does not exist: {Root}", generationRoot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error discovering Java inputs under {Root}", generationRoot);
        }
        return inputs;
    }

    private async Task<bool> RunAPIViewProcessorAsync(string processorJar, List<string> inputs, string outDir, CancellationToken ct)
    {
        var joined = string.Join(',', inputs);
        var psi = new ProcessStartInfo
        {
            FileName = "java",
            Arguments = $"-jar \"{processorJar}\" \"{joined}\" \"{outDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = outDir
        };
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            return false;
        }
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
        {
            _logger.LogDebug("APIView processor exit {Code}: {Err}", proc.ExitCode, Truncate(stderr, 400));
            return false;
        }
        _logger.LogDebug("APIView processor stdout (truncated): {Out}", Truncate(stdout, 400));
        return true;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
        {
            return s;
        }
        return s[..max] + "...";
    }

    public override Task<string?> GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct)
    {
        try
        {
            // In azure-sdk-for-java layout, generated code lives under:
            //   <pkgRoot>/azure-<package>-<service>/src
            // Customizations (single root) live under parallel directory:
            //   <pkgRoot>/azure-<package>-<service>/customization/src/main/java
            // Example (document intelligence):
            //   generated root: .../azure-ai-documentintelligence/src
            //   customization root: .../azure-ai-documentintelligence/customization/src/main/java

            var packageRoot = Directory.GetParent(generationRoot)?.FullName; // parent of 'src'
            if (!string.IsNullOrEmpty(packageRoot) && Directory.Exists(packageRoot))
            {
                // canonical customization root: <packageRoot>/customization/src/main/java
                var customizationSourceRoot = Path.Combine(packageRoot, CustomizationDirName, "src", "main", "java");
                if (Directory.Exists(customizationSourceRoot))
                {
                    return Task.FromResult<string?>(customizationSourceRoot);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve Java customization root from generationRoot '{GenerationRoot}'", generationRoot);
        }
        return Task.FromResult<string?>(null);
    }

    public override Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(ClientUpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct)
    {
        // Stub: TODO no impacted files
        return Task.FromResult(new List<CustomizationImpact>());
    }

    public override Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct)
    {
    // Stub: create a trivial placeholder patch for each impacted file.
        var proposals = impacts
            .Select(i => new PatchProposal
            {
                File = i.File,
                Diff = $"--- a/{i.File}\n+++ b/{i.File}\n// TODO: computed diff placeholder\n"
            })
            .ToList();
        return Task.FromResult(proposals);
    }
}
