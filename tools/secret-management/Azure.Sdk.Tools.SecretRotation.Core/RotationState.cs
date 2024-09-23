namespace Azure.Sdk.Tools.SecretRotation.Core;

public enum RotationState
{
    Error,
    UpToDate,
    Rotate,
    Warning,
    Expired,
}
