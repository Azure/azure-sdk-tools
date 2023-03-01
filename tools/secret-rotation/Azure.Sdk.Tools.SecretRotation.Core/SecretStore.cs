namespace Azure.Sdk.Tools.SecretRotation.Core;

public abstract class SecretStore
{
    public virtual bool CanRead => false;

    public virtual bool CanOriginate => false;

    public virtual bool CanAnnotate => false;

    public virtual bool CanWrite => false;

    public virtual bool CanRevoke => false;

    public virtual string? Name { get; set; }

    public virtual bool UpdateAfterPrimary { get; set; }

    public virtual bool IsPrimary { get; set; }

    public virtual bool IsOrigin { get; set; }



    // Read

    public virtual Task<SecretState> GetCurrentStateAsync()
    {
        throw new NotImplementedException();
    }

    public virtual Task<IEnumerable<SecretState>> GetRotationArtifactsAsync()
    {
        throw new NotImplementedException();
    }

    // Annotate

    public virtual Task MarkRotationCompleteAsync(SecretValue secretValue, DateTimeOffset? revokeAfterDate, bool whatIf)
    {
        throw new NotImplementedException();
    }

    // Revocation

    public virtual Func<Task>? GetRevocationActionAsync(SecretState secretState, bool whatIf)
    {
        throw new NotImplementedException();
    }

    // Originate

    public virtual Task<SecretValue> OriginateValueAsync(SecretState currentState, DateTimeOffset expirationDate,
        bool whatIf)
    {
        throw new NotImplementedException();
    }

    // Write

    public virtual Task WriteSecretAsync(SecretValue secretValue, SecretState currentState,
        DateTimeOffset? revokeAfterDate, bool whatIf)
    {
        throw new NotImplementedException();
    }
}
