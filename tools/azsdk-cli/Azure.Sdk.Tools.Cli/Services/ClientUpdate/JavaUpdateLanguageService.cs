// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System;

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
        var external = await RunExternalJavaApiDiffAsync(oldGenerationPath!, newGenerationPath!, CancellationToken.None);
        return external;
    }

    /// <summary>
    /// Invoke an external Java-based API diff tool (for example apidiff.jar) to compare two generated API roots.
    /// The tool must output a JSON array of ApiChange objects, e.g.:
    /// [{"kind":"MethodAdded","symbol":"...","detail":"...","meta":{...}}].
    /// If the tool is not present, exits with an error, or the output cannot be parsed, this method returns an empty list.
    /// </summary>
    /// <param name="oldPath">File-system path to the old generation root.</param>
    /// <param name="newPath">File-system path to the new generation root.</param>
    /// <param name="ct">Cancellation token to cancel the external process and reads.</param>
    /// <returns>A list of parsed ApiChange instances, or an empty list on error or when the tool is unavailable.</returns>
    /// <remarks>
    /// The implementation launches Java with the diff tool jar and parses its JSON stdout. Failures and non-zero
    /// exit codes are logged at Debug level and treated as "no changes" to preserve robustness of calling code.
    /// </remarks>
    protected virtual async Task<List<ApiChange>> RunExternalJavaApiDiffAsync(string oldPath, string newPath, CancellationToken ct)
    {
        var results = new List<ApiChange>();
        var toolJar = ResolveJar();
        if (toolJar == null)
        {
            // Optional on-demand build if env flag set
            if (IsAutoBuildEnabled())
            {
                toolJar = TryAutoBuildJar(ct);
            }
            _logger.LogDebug("Java API diff jar not found (skipping diff). Set APIDIFF_JAR or place jar at conventional path.");
            return results;
        }
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "java",
            Arguments = $"-jar \"{toolJar}\" --old \"{oldPath}\" --new \"{newPath}\" --format json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = newPath
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc == null)
        {
            return results;
        }
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
        {
            _logger.LogDebug("Java API diff tool exit {code}: {err}", proc.ExitCode, stderr);
            return results;
        }
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<ApiChange>>(stdout, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (parsed != null)
            {
                results.AddRange(parsed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to deserialize Java API diff tool JSON output");
        }
        return results;
    }

    // Auto-build logic removed for simplicity; jar must be pre-built or supplied via APIDIFF_JAR.

    private string? ResolveJar()
    {
        // 1. Explicit env var wins
        var env = Environment.GetEnvironmentVariable("APIDIFF_JAR");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return env;
        }

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 2. Conventional deployed location next to CLI binaries
        var conventional = Path.Combine(baseDir, "tools", "java", "apidiff", "apidiff.jar");
        if (File.Exists(conventional))
        {
            return conventional;
        }

        // 3. Common source-relative target jars (developer scenarios). We DO NOT build, only check.
        // Walk a few parent levels to allow running from bin/Debug/net8.0
        var candidates = new List<string>();
        var dir = new DirectoryInfo(baseDir);
        for (int i = 0; i < 6 && dir != null; i++)
        {
            // Tools/APIDiffTool/target/<name>-jar-with-dependencies.jar
            var toolsTarget = Path.Combine(dir.FullName, "Tools", "APIDiffTool", "target");
            if (Directory.Exists(toolsTarget))
            {
                candidates.AddRange(Directory.GetFiles(toolsTarget, "*-jar-with-dependencies.jar"));
            }
            // tools/java/apidiff/target/... alternate layout
            var altTarget = Path.Combine(dir.FullName, "tools", "java", "apidiff", "target");
            if (Directory.Exists(altTarget))
            {
                candidates.AddRange(Directory.GetFiles(altTarget, "*-jar-with-dependencies.jar"));
                // also conventional after manual copy
                var altConventional = Path.Combine(dir.FullName, "tools", "java", "apidiff", "apidiff.jar");
                if (File.Exists(altConventional))
                {
                    candidates.Add(altConventional);
                }
            }
            dir = dir.Parent;
        }

        // Prefer exact conventional name if present among candidates; else first match.
        var exact = candidates.FirstOrDefault(p => Path.GetFileName(p).Equals("apidiff.jar", StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }
        return candidates.FirstOrDefault();
    }

    private bool IsAutoBuildEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("APIDIFF_AUTOBUILD"), "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("APIDIFF_AUTOBUILD"), "true", StringComparison.OrdinalIgnoreCase);

    private string? TryAutoBuildJar(CancellationToken ct)
    {
        try
        {
            // Locate repo root by searching for Azure.Sdk.Tools.Cli.csproj
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            DirectoryInfo? root = null;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Azure.Sdk.Tools.Cli.csproj"))) { root = dir; break; }
                dir = dir.Parent;
            }
            if (root == null)
            {
                return null;
            }
            var pom = Path.Combine(root.FullName, "Tools", "APIDiffTool", "pom.xml");
            if (!File.Exists(pom))
            {
                return null;
            }
            var mvnExe = "mvn" + (OperatingSystem.IsWindows() ? ".cmd" : "");
            var psi = new ProcessStartInfo
            {
                FileName = mvnExe,
                Arguments = "-q -DskipTests package",
                WorkingDirectory = Path.GetDirectoryName(pom)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return null;
            }
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            proc.WaitForExit();
            var stderr = stderrTask.Result;
            if (proc.ExitCode != 0)
            {
                _logger.LogDebug("apidiff autobuild failed exit {Code}: {Err}", proc.ExitCode, stderr);
                return null;
            }
            var targetDir = Path.Combine(Path.GetDirectoryName(pom)!, "target");
            var shaded = Directory.Exists(targetDir) ? Directory.GetFiles(targetDir, "*-jar-with-dependencies.jar").FirstOrDefault() : null;
            if (shaded == null)
            {
                return null;
            }
            var conventional = Path.Combine(AppContext.BaseDirectory, "tools", "java", "apidiff", "apidiff.jar");
            Directory.CreateDirectory(Path.GetDirectoryName(conventional)!);
            File.Copy(shaded, conventional, overwrite: true);
            return File.Exists(conventional) ? conventional : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed APIDiff auto-build");
            return null;
        }
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
