// Clean implementation of JavaUpdateLanguageService (integrated diff mode)
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

public class JavaUpdateLanguageService : ClientUpdateLanguageServiceBase
{
    private readonly ILogger<JavaUpdateLanguageService> _logger;
    private const string CustomizationDirName = "customization";

    public JavaUpdateLanguageService(ILanguageSpecificCheckResolver languageSpecificCheckResolver, ILogger<JavaUpdateLanguageService> logger) : base(languageSpecificCheckResolver)
    {
        _logger = logger;
    }

    public override string SupportedLanguage => "java";

    public override async Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
    {
        if (string.IsNullOrWhiteSpace(oldGenerationPath) || string.IsNullOrWhiteSpace(newGenerationPath))
        {
            throw new InvalidOperationException("Java API diff requires both oldGenerationPath and newGenerationPath.");
        }
        return await RunApiViewDiffAsync(oldGenerationPath, newGenerationPath, CancellationToken.None);
    }

    private async Task<List<ApiChange>> RunApiViewDiffAsync(string oldPath, string newPath, CancellationToken ct)
    {
        var result = new List<ApiChange>();
        try
        {
            var processorJar = await JavaApiViewJarBuilder.BuildProcessorJarAsync(_logger, ct);
            if (string.IsNullOrEmpty(processorJar))
            {
                _logger.LogDebug("Processor jar build returned null");
                return result;
            }
            var oldInputs = DiscoverJavaInputs(oldPath);
            var newInputs = DiscoverJavaInputs(newPath);
            if (oldInputs.Count == 0 || newInputs.Count == 0)
            {
                _logger.LogDebug("No inputs for diff (old={Old}, new={New})", oldInputs.Count, newInputs.Count);
                return result;
            }
            var tempRoot = Path.Combine(Path.GetTempPath(), "apiview-diff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var diffOut = Path.Combine(tempRoot, "out");
            Directory.CreateDirectory(diffOut);
            if (!await RunAPIViewProcessorDiffAsync(processorJar, oldInputs, newInputs, diffOut, ct))
            {
                return result;
            }
            var diffPath = Path.Combine(diffOut, "apiview-diff.json");
            if (!File.Exists(diffPath))
            {
                _logger.LogDebug("Diff output file not found: {Path}", diffPath);
                return result;
            }
            await using var fs = File.OpenRead(diffPath);
            using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("changes", out var changesElement))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new ApiChangeJsonConverter());
                var changes = JsonSerializer.Deserialize<List<ApiChange>>(changesElement.GetRawText(), options);
                if (changes != null)
                {
                    result = changes;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed running integrated diff mode");
        }
        return result;
    }

    private async Task<bool> RunAPIViewProcessorDiffAsync(string processorJar, List<string> oldInputs, List<string> newInputs, string outDir, CancellationToken ct)
    {
        var oldJoined = string.Join(',', oldInputs);
        var newJoined = string.Join(',', newInputs);
        var psi = new ProcessStartInfo
        {
            FileName = "java",
            Arguments = $"-jar \"{processorJar}\" --diff --old \"{oldJoined}\" --new \"{newJoined}\" --out \"{outDir}\"",
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
            _logger.LogDebug("Processor --diff exit {Code}: {Err}", proc.ExitCode, Truncate(stderr, 400));
            return false;
        }
        _logger.LogDebug("Processor --diff stdout (trunc): {Out}", Truncate(stdout, 400));
        return true;
    }

    private class ApiChangeJsonConverter : JsonConverter<ApiChange>
    {
        public override ApiChange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            
            var apiChange = new ApiChange();

            // Map changeType to Kind
            if (root.TryGetProperty("changeType", out var changeTypeElement))
            {
                apiChange.Kind = changeTypeElement.GetString() ?? string.Empty;
            }

            // Extract before/after for Detail
            string? before = null, after = null;
            if (root.TryGetProperty("before", out var beforeElement))
            {
                before = beforeElement.GetString();
            }
            if (root.TryGetProperty("after", out var afterElement))
            {
                after = afterElement.GetString();
            }

            // Build Detail from before/after
            if (!string.IsNullOrEmpty(before) && !string.IsNullOrEmpty(after))
            {
                apiChange.Detail = $"{before} -> {after}";
            }
            else
            {
                apiChange.Detail = before ?? after ?? string.Empty;
            }

            // Extract Symbol from meta
            if (root.TryGetProperty("meta", out var metaElement))
            {
                // Try to get the best symbol name
                string? symbolFromMeta = null;
                if (metaElement.TryGetProperty("methodName", out var methodNameElement))
                {
                    symbolFromMeta = methodNameElement.GetString();
                }
                else if (metaElement.TryGetProperty("fieldName", out var fieldNameElement))
                {
                    symbolFromMeta = fieldNameElement.GetString();
                }
                else if (metaElement.TryGetProperty("fqn", out var fqnElement))
                {
                    symbolFromMeta = fqnElement.GetString();
                }
                
                apiChange.Symbol = symbolFromMeta ?? string.Empty;

                // Flatten all meta properties into Metadata dictionary
                foreach (var property in metaElement.EnumerateObject())
                {
                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.Array => string.Join(",", property.Value.EnumerateArray().Select(e => e.GetString())),
                        _ => property.Value.GetRawText()
                    };
                    
                    if (!string.IsNullOrEmpty(value))
                    {
                        apiChange.Metadata[property.Name] = value;
                    }
                }
            }

            // Add category to metadata if present
            if (root.TryGetProperty("category", out var categoryElement))
            {
                var category = categoryElement.GetString();
                if (!string.IsNullOrEmpty(category))
                {
                    apiChange.Metadata["category"] = category;
                }
            }

            return apiChange;
        }

        public override void Write(Utf8JsonWriter writer, ApiChange value, JsonSerializerOptions options)
        {
            throw new NotImplementedException("Writing ApiChange to JSON is not supported");
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

    private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "...";
    public override Task<string?> GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct)
    {
        try
        {
            var packageRoot = Directory.GetParent(generationRoot)?.FullName;
            if (!string.IsNullOrEmpty(packageRoot) && Directory.Exists(packageRoot))
            {
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
        return Task.FromResult(new List<CustomizationImpact>()); // future enhancement
    }

    public override Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct)
    {
        var proposals = impacts.Select(i => new PatchProposal
        {
            File = i.File,
            Diff = $"--- a/{i.File}\n+++ b/{i.File}\n// TODO: computed diff placeholder\n"
        }).ToList();
        return Task.FromResult(proposals);
    }
}
