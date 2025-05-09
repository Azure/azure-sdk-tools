using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Services;

public interface ICommandFormatter
{
    FormattedResponse Format(CommandResponse response);
}

public class PlainTextFormatter : ICommandFormatter
{
    public FormattedResponse Format(CommandResponse response)
    {
        return new FormattedResponse(response.AsPlainText());
    }
}

public class JsonFormatter : ICommandFormatter
{
    public FormattedResponse Format(CommandResponse response)
    {
        return new FormattedResponse(response.AsJson());
    }
}

public class McpFormatter : ICommandFormatter
{
    public FormattedResponse Format(CommandResponse response)
    {
        return new FormattedResponse(response.AsMcp());
    }
}

public class FormattedResponse(string value)
{
    private string _value { get; set; } = value;

    public static implicit operator string(FormattedResponse response)
    {
        return response._value;
    }

    public override string ToString()
    {
        return _value;
    }
}
