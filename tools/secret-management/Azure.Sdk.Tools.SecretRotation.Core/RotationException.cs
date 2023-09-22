namespace Azure.Sdk.Tools.SecretRotation.Core;

public class RotationException : Exception
{
    public RotationException(string message, Exception? innerException = default) : base(message, innerException)
    {
    }
}
