namespace Azure.Sdk.Tools.SecretRotation.Core;

public class SecretValue
{
    public string? OperationId { get; set; }

    public DateTimeOffset? ExpirationDate { get; set; }

    // TODO: Use SecureString if possible
    public string Value { get; set; } = string.Empty;

    /// <summary>
    ///     A state object created by primary stores that can be used in post-rotation annotation.
    /// </summary>
    /// <remarks>
    ///     This is a round-trip object that will be returned to the primary IStateStore once all other stores are updated.
    ///     For example, a Key Vault primary may store the original KeyVaultCertificate reference in PrimaryState.
    /// </remarks>
    public object? PrimaryState { get; set; }

    // During propagation, origin and secondary stores can add tags to be written to the primary store during the completion/annotation phase.
    // These tags are used during revocation to ensure the appropriate origin or downstream resource is revoked.
    public Dictionary<string, string> Tags { get; init; } = new();
}
