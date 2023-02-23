namespace Azure.Sdk.Tools.SecretRotation.Cli;

public class RotationCliException : Exception
{
    public RotationCliException(string message) : base(message)
    {
    }
}
