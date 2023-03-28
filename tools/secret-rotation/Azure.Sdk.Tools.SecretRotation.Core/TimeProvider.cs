namespace Azure.Sdk.Tools.SecretRotation.Core;

public class TimeProvider
{
    public virtual DateTimeOffset GetCurrentDateTimeOffset()
    {
        return DateTimeOffset.UtcNow;
    }
}
