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
            var payload = await JsonSerializer.DeserializeAsync<RawApiDiffResult>(fs, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
            if (payload?.Changes != null)
            {
                result = payload.Changes.Select(MapRawChange).ToList();
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

    private static ApiChange MapRawChange(RawApiChange raw)
    {
        // New Java diff schema fields:
        // changeType, before, after, category, meta{ symbolKind,fqn,methodName,fieldName,signature,visibility,returnType,parameterTypes,parameterNames,deprecated,paramNameChange }
        var apiChange = new ApiChange();

        // Map high-level fields. We'll surface changeType as Kind, and use before/after to construct Symbol/Detail heuristically.
        apiChange.Kind = raw.ChangeType ?? string.Empty;

        // Determine a representative symbol name from meta.
        string? symbolFromMeta = raw.Meta?.MethodName ?? raw.Meta?.FieldName ?? raw.Meta?.Fqn;
        apiChange.Symbol = symbolFromMeta ?? string.Empty;

        // Detail: show a concise before -> after form if both exist; else whichever exists.
        if (!string.IsNullOrEmpty(raw.Before) && !string.IsNullOrEmpty(raw.After))
        {
            apiChange.Detail = $"{raw.Before} -> {raw.After}";
        }
        else
        {
            apiChange.Detail = raw.Before ?? raw.After ?? string.Empty;
        }

        if (raw.Meta != null)
        {
            void Add(string key, object? value)
            {
                if (value == null)
                {
                    return;
                }
                var str = value switch
                {
                    string s => s,
                    string[] arr => string.Join(",", arr),
                    bool b => b.ToString().ToLowerInvariant(),
                    int i => i.ToString(),
                    _ => value.ToString() ?? string.Empty
                };
                if (!string.IsNullOrEmpty(str))
                {
                    apiChange.Metadata[key] = str;
                }
            }

            Add("symbolKind", raw.Meta.SymbolKind);
            Add("fqn", raw.Meta.Fqn);
            Add("methodName", raw.Meta.MethodName);
            Add("fieldName", raw.Meta.FieldName);
            Add("signature", raw.Meta.Signature);
            Add("visibility", raw.Meta.Visibility);
            Add("returnType", raw.Meta.ReturnType);
            Add("parameterTypes", raw.Meta.ParameterTypes);
            Add("parameterNames", raw.Meta.ParameterNames);
            Add("deprecated", raw.Meta.Deprecated);
            Add("paramNameChange", raw.Meta.ParamNameChange);
        }
        if (!string.IsNullOrEmpty(raw.Category))
        {
            apiChange.Metadata["category"] = raw.Category!;
        }
        return apiChange;
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

internal class RawApiDiffResult
{
    [JsonPropertyName("schemaVersion")] public string? SchemaVersion { get; set; }
    [JsonPropertyName("changes")] public List<RawApiChange> Changes { get; set; } = new();
}

internal class RawApiChange
{
    // Updated to new Java diff output schema
    [JsonPropertyName("changeType")] public string? ChangeType { get; set; }
    [JsonPropertyName("before")] public string? Before { get; set; }
    [JsonPropertyName("after")] public string? After { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("meta")] public RawApiChangeMeta? Meta { get; set; }
}

internal class RawApiChangeMeta
{
    [JsonPropertyName("symbolKind")] public string? SymbolKind { get; set; }
    [JsonPropertyName("fqn")] public string? Fqn { get; set; }
    [JsonPropertyName("methodName")] public string? MethodName { get; set; }
    [JsonPropertyName("fieldName")] public string? FieldName { get; set; }
    [JsonPropertyName("signature")] public string? Signature { get; set; }
    [JsonPropertyName("visibility")] public string? Visibility { get; set; }
    [JsonPropertyName("returnType")] public string? ReturnType { get; set; }
    [JsonPropertyName("parameterTypes")] public string[]? ParameterTypes { get; set; }
    [JsonPropertyName("parameterNames")] public string[]? ParameterNames { get; set; }
    [JsonPropertyName("deprecated")] public bool? Deprecated { get; set; }
    [JsonPropertyName("paramNameChange")] public bool? ParamNameChange { get; set; }
}
