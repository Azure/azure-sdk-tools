using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Services;

public interface ICommandFormatter
{
    string Format(BaseCommandResponse response);
}

public abstract class BaseFormatter : ICommandFormatter
{
    public abstract string Format(BaseCommandResponse response);
}

public class PlainTextFormatter : BaseFormatter
{
    public override string Format(BaseCommandResponse response)
    {
        return response.ToPlainText();
    }
}

public class JsonFormatter : BaseFormatter
{
    protected readonly JsonSerializerOptions prettySerializerOptions = new()
    {
        WriteIndented = true,
    };

    public override string Format(BaseCommandResponse response)
    {
        return JsonSerializer.Serialize(response, response.GetType(), prettySerializerOptions);
    }
}

public class McpFormatter : BaseFormatter
{
    protected readonly JsonSerializerOptions compressSerializerOptions = new()
    {
        WriteIndented = false,
    };

    public override string Format(BaseCommandResponse response)
    {
        return JsonSerializer.Serialize(response, response.GetType(), compressSerializerOptions);
    }
}
