namespace Azure.Sdk.Tools.Cli.Models;

public static class ApiReviewPackageType
{
    public static readonly string[] SupportedValues = ["mgmt", "client", "spring", "functions"];

    public static string Normalize(string packageType) => packageType.ToLowerInvariant() switch
    {
        "mgmt" => "mgmt",
        "client" or "spring" or "functions" => "client",
        _ => throw new ArgumentException(
            $"Unsupported package type '{packageType}'. Supported values: {string.Join(", ", SupportedValues)}.",
            nameof(packageType))
    };
}
