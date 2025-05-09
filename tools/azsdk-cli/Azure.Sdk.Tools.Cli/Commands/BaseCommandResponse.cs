using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Commands;

public abstract class BaseCommandResponse()
{
    private ICommandFormatter formatter;
    private bool isInitialized = false;

    public void Initialize(ICommandFormatter _formatter)
    {
        formatter = _formatter;
        isInitialized = true;
    }

    public override string ToString()
    {
        if (!isInitialized)
        {
            throw new InvalidOperationException("Command response has not been initialized");
        }
        return formatter.Format(this);
    }

    public abstract string ToPlainText();
}