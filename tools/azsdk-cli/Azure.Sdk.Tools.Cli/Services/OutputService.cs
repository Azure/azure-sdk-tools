using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IOutputService
{
    string Format(object response);
    string ValidateAndFormat<T>(string response);
    void Output(object output);
    void Output(string output);
    void OutputError(object output);
    void OutputError(string output);
}

public class OutputService : IOutputService
{
    private OutputModes OutputMode { get; set; }

    public OutputService(OutputModes outputMode)
    {
        OutputMode = outputMode;
        if (OutputMode == OutputModes.Mcp)
        {
            serializerOptions.WriteIndented = false;
        }
    }

    private readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true,
    };

    public string Format(object response)
    {
        if (OutputMode != OutputModes.Plain)
        {
            return JsonSerializer.Serialize(response, serializerOptions);
        }

        var elementType = response.GetType().IsGenericType ? response.GetType().GetGenericArguments()[0] : null;

        if (response is System.Collections.IEnumerable enumerable && response is not string)
        {
            var separator = "--------------------------------------------------------------------------------" + Environment.NewLine;
            if (elementType == null
                || elementType.IsPrimitive || elementType == typeof(decimal) || elementType == typeof(string))
            {
                separator = "";
            }
            var outputs = enumerable.Cast<object>().Select(item => item?.ToString());
            return string.Join(separator + Environment.NewLine, outputs);
        }

        return response.ToString();
    }

    public string ValidateAndFormat<T>(string response)
    {
        var obj = JsonSerializer.Deserialize<T>(response);
        if (obj == null)
        {
            throw new InvalidOperationException($"Deserializing response resulted in null object: {response}");
        }
        return Format(obj);
    }

    public void Output(object output)
    {
        Output(Format(output));
    }

    public virtual void Output(string output)
    {
        Console.WriteLine(output);
    }

    public virtual void OutputError(object output)
    {
        OutputError(Format(output));
    }

    public virtual void OutputError(string output)
    {
        Console.Error.WriteLine(output);
    }
}
