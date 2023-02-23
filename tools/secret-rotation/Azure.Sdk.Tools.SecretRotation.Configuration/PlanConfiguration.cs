using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.SecretRotation.Configuration;

public class PlanConfiguration
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("rotationThreshold")]
    public TimeSpan? RotationThreshold { get; set; }

    [JsonPropertyName("rotationPeriod")]
    public TimeSpan? RotationPeriod { get; set; }

    [JsonPropertyName("revokeAfterPeriod")]
    public TimeSpan? RevokeAfterPeriod { get; set; }

    [JsonPropertyName("stores")]
    public StoreConfiguration[] StoreConfigurations { get; set; } = Array.Empty<StoreConfiguration>();

    public static PlanConfiguration FromFile(string path)
    {
        string fileContents = GetFileContents(path);

        PlanConfiguration configuration = ParseConfiguration(path, fileContents);

        if (string.IsNullOrEmpty(configuration.Name))
        {
            configuration.Name = Path.GetFileNameWithoutExtension(path);
        }

        return configuration;
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

    private static PlanConfiguration ParseConfiguration(string filePath, string fileContents)
    {
        try
        {
            PlanConfiguration planConfiguration = JsonSerializer.Deserialize<PlanConfiguration>(fileContents)
                ?? throw new RotationConfigurationException($"Error reading configuration file '{filePath}'. Configuration deserialized to null.");

            return planConfiguration;
        }
        catch (JsonException ex)
        {
            throw new RotationConfigurationException($"Error deserializing json from file '{filePath}'.", ex);
        }
    }
}
