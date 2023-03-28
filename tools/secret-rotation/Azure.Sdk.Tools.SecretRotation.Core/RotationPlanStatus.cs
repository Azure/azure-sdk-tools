using Azure.Sdk.Tools.SecretRotation.Core;

namespace Azure.Sdk.Tools.SecretRotation.Core;

public class RotationPlanStatus
{
    public bool Expired { get; set; }

    public bool ThresholdExpired { get; set; }

    public bool RequiresRevocation { get; set; }

    public SecretState? PrimaryStoreState { get; set; }

    public IReadOnlyList<SecretState>? SecondaryStoreStates { get; set; }

    public DateTimeOffset? ExpirationDate { get; set; }

    public RotationException? Exception { get; set; }
}
