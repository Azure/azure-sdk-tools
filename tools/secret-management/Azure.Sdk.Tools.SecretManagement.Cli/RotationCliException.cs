namespace Azure.Sdk.Tools.SecretManagement.Cli;

public class RotationCliException : Exception
{
    public RotationCliException(string message) : base(message)
    {
    }
}
