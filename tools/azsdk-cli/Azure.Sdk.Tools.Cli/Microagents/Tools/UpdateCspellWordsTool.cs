using System.ComponentModel;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

public record UpdateCspellWordsInput([property: Description("List of words to add to the cspell.json words list")] List<string> Words);
public record UpdateCspellWordsOutput([property: Description("Success message with number of words added")] string Message);

public class UpdateCspellWordsTool(string baseDir) : AgentTool<UpdateCspellWordsInput, UpdateCspellWordsOutput>
{
    public override string Name { get; init; } = "UpdateCspellWords";
    public override string Description { get; init; } = "Add words to the cspell.json words list";

    public override async Task<UpdateCspellWordsOutput> Invoke(UpdateCspellWordsInput input, CancellationToken ct)
    {
        var cspellRelativePath = Path.Combine(".vscode", "cspell.json");
        
        if (!ToolHelpers.TryGetSafeFullPath(baseDir, cspellRelativePath, out var cspellPath))
        {
            throw new ArgumentException("The cspell.json path is invalid or outside the allowed base directory.");
        }

        if (!File.Exists(cspellPath))
        {
            throw new FileNotFoundException($"cspell.json not found at {cspellPath}");
        }

        var cspellContent = await File.ReadAllTextAsync(cspellPath, ct);
        var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(cspellContent) 
            ?? new Dictionary<string, object>();

        var existingWords = GetExistingWords(configDict);
        var wordsToAdd = input.Words.Where(w => !existingWords.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
        
        existingWords.AddRange(wordsToAdd);
        configDict["words"] = existingWords;

        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true, 
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
        };
        var updatedContent = JsonSerializer.Serialize(configDict, options);
        await File.WriteAllTextAsync(cspellPath, updatedContent, ct);

        return new UpdateCspellWordsOutput($"Successfully added {wordsToAdd.Count} words to cspell.json");
    }

    private static List<string> GetExistingWords(Dictionary<string, object> configDict)
    {
        if (!configDict.ContainsKey("words")) 
        {
            return new List<string>();
        }
        
        var wordsElement = (JsonElement)configDict["words"];
        return wordsElement.ValueKind == JsonValueKind.Array
            ? wordsElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : new List<string>();
    }
}