namespace Azure.Sdk.Tools.SecretRotation.Core;

public class SecretState
{
    public string? Id { get; set; }

    public string? OperationId { get; set; }

    public DateTimeOffset? ExpirationDate { get; set; }

    public DateTimeOffset? RevokeAfterDate { get; set; }

    public int? StatusCode { get; set; }

    public string? ErrorMessage { get; set; }

    public IDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

    public string? Value { get; set; }
}
