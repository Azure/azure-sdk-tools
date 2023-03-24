using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.SecretRotation.Configuration;

public class PlanConfiguration
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        AllowTrailingCommas  = true,
        Converters =
        {
            new JsonStringEnumConverter()
        },
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly Regex schemaPattern = new (@"/(?<version>.+?)/plan\.json", RegexOptions.IgnoreCase);

    public string Name { get; set; } = string.Empty;

    public TimeSpan? RotationThreshold { get; set; }

    public TimeSpan? RotationPeriod { get; set; }

    public TimeSpan? RevokeAfterPeriod { get; set; }

    [JsonPropertyName("stores")]
    public StoreConfiguration[] StoreConfigurations { get; set; } = Array.Empty<StoreConfiguration>();

    public string[] Tags { get; set; } = Array.Empty<string>();

    public static bool TryLoadFromFile(string path, out PlanConfiguration? configuration)
    {
        string fileContents = GetFileContents(path);

        configuration = ParseConfiguration(path, fileContents);

        if (configuration == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(configuration.Name))
        {
            configuration.Name = Path.GetFileNameWithoutExtension(path);
        }

        return true;
    }

    private static string GetFileContents(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return File.ReadAllText(file.FullName);
        }
        catch (Exception ex)
        {
            throw new RotationConfigurationException($"Error reading configuration file '{path}'.", ex);
        }
    }

    private static PlanConfiguration? ParseConfiguration(string filePath, string fileContents)
    {
        try
        {
            JsonDocument document = JsonDocument.Parse(fileContents, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("$schema", out JsonElement schemaElement)
                || schemaElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string schema = schemaElement.ToString();
            Match schemaMatch = schemaPattern.Match(schema);
            if (!schemaMatch.Success)
            {
                // We expect to recognize the url in the $schema property. This allows us to ignore json files in the
                // config path that aren't intended to be plan configurations.
                return null;
            }

            PlanConfiguration planConfiguration = document.Deserialize<PlanConfiguration>(jsonOptions)
                ?? throw new RotationConfigurationException($"Error reading configuration file '{filePath}'. Configuration deserialized to null.");

            return planConfiguration;
        }
        catch (JsonException ex)
        {
            throw new RotationConfigurationException($"Error deserializing json from file '{filePath}'.", ex);
        }
    }
}
