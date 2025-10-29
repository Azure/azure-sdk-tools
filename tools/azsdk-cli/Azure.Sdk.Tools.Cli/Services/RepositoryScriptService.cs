using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IRepositoryScriptService
{
    Task<string> GetCommand(string commandName, string packagePath, CancellationToken ct);
    Task<bool> HasImplementation(string commandName, string packagePath, CancellationToken ct);
    Task<(bool invoked, ProcessResult result)> TryInvoke(
        string commandName,
        string packagePath,
        OrderedDictionary args,
        bool invokeFromRepoRoot = true,
        CancellationToken ct = default
    );
    Task<(bool invoked, ProcessResult result)> TryInvoke(string commandName, string packagePath, OrderedDictionary args, CancellationToken ct = default);
}

public class RepoCommandContract
{
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;
}

/// <summary>
/// Provides repository-specific script discovery and execution by loading the automation config,
/// caching command mappings per package path, and invoking the associated PowerShell scripts.
/// </summary>
/// <param name="logger">The logger used to record informational and warning messages.</param>
/// <param name="gitHelper">Helper that locates the repository root for a given package path.</param>
/// <param name="powershellHelper">Helper responsible for running PowerShell commands.</param>
public class RepositoryScriptService(
    ILogger<RepositoryScriptService> logger,
    IGitHelper gitHelper,
    IPowershellHelper powershellHelper
) : IRepositoryScriptService
{
    public string ScriptConfig = Path.Join("eng", "azsdk-cli-command-overrides.json");

    private readonly Dictionary<string, Dictionary<string, string>> commandCache = [];

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private async Task Load(string repoRoot, CancellationToken ct)
    {
        if (commandCache.TryGetValue(repoRoot, out var _))
        {
            return;
        }

        var contractPath = Path.Join(repoRoot, ScriptConfig);
        if (File.Exists(contractPath))
        {
            var json = await File.ReadAllTextAsync(contractPath, ct);
            var contract = JsonSerializer.Deserialize<List<RepoCommandContract>>(json, jsonOptions);

            if (contract == null)
            {
                logger.LogWarning("Failed to deserialize contract at {ContractPath}", contractPath);
                return;
            }

            foreach (var command in contract)
            {
                foreach (var tag in command.Tags)
                {
                    if (!commandCache.ContainsKey(repoRoot))
                    {
                        commandCache[repoRoot] = new Dictionary<string, string>();
                    }
                    commandCache[repoRoot][tag] = Path.Join(command.Command);
                }
            }
        }
    }

    public async Task<string> GetCommand(string commandName, string repoRoot, CancellationToken ct)
    {
        await Load(repoRoot, ct);

        if (string.IsNullOrEmpty(repoRoot) ||
            !commandCache.TryGetValue(repoRoot, out var commandMap) ||
            !commandMap.TryGetValue(commandName, out string? value))
        {
            return null;
        }

        if (!File.Exists(Path.Join(repoRoot, value)))
        {
            throw new Exception($"Script override '{value}' for command '{commandName}' in '{ScriptConfig}' does not exist in the repository");
        }

        return value;
    }

    public async Task<bool> HasImplementation(string commandName, string repoRoot, CancellationToken ct)
    {
        await Load(repoRoot, ct);

        if (string.IsNullOrEmpty(repoRoot) ||
            !commandCache.TryGetValue(repoRoot, out var commandMap) ||
            !commandMap.TryGetValue(commandName, out string? _))
        {
            return false;
        }

        return true;
    }

    public async Task<(bool invoked, ProcessResult result)> TryInvoke(
        string commandName,
        string packagePath,
        OrderedDictionary args,
        bool invokeFromRepoRoot = true,
        CancellationToken ct = default
    )
    {
        var repoRoot = gitHelper.DiscoverRepoRoot(packagePath);
        var scriptPath = await GetCommand(commandName, repoRoot, ct);
        if (scriptPath == null)
        {
            return (false, new());
        }

        string workingDirectory = "";
        if (invokeFromRepoRoot)
        {
            scriptPath = Path.Join(repoRoot, scriptPath);
            workingDirectory = repoRoot;
        }

        var paramJson = JsonSerializer.Serialize(args, jsonOptions);
        var command = $"$params = ('{paramJson}' | ConvertFrom-Json -AsHashtable); & {scriptPath} @params";

        var options = new PowershellOptions(args: [command], workingDirectory: workingDirectory);
        var result = await powershellHelper.Run(options, ct);

        if (result.ExitCode != 0)
        {
            logger.LogError("Command '{CommandName}' in package path '{PackagePath}' failed with exit code {ExitCode}. Output: {Output}",
                commandName, packagePath, result.ExitCode, result.Output);
        }

        return (true, result);
    }


    public async Task<(bool invoked, ProcessResult result)> TryInvoke(string commandName, string packagePath, OrderedDictionary args, CancellationToken ct)
    {
        return await TryInvoke(commandName, packagePath, args, true, ct);
    }
}
