using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface IRawOutputHelper
{
    void OutputConsole(string output);
    void OutputConsoleInfo(string output);
    void OutputConsoleWarning(string output);
    void OutputConsoleError(string output);
}

public interface IOutputHelper
{
    string Format(object response);
    string ValidateAndFormat<T>(string response);
    void Output(object output);
    void Output(string output);
    void OutputError(object output);
    void OutputError(string output);
    void OutputCommandResponse(CommandResponse output);
}

public class OutputHelper : IOutputHelper, IRawOutputHelper
{
    private OutputModes OutputMode { get; set; }

    public enum StreamType
    {
        Stdout,
        Stderr
    }

    public enum OutputModes
    {
        Json,
        Plain,
        Mcp,
        Hidden
    }

    private readonly SemaphoreSlim outputSemaphore = new(1, 1);
    public List<(StreamType Stream, string Output)> Outputs { get; } = [];

    private readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true,
    };

    public OutputHelper(OutputModes outputMode = OutputModes.Hidden)
    {
        OutputMode = outputMode;
        if (OutputMode == OutputModes.Mcp)
        {
            serializerOptions.WriteIndented = false;
        }
    }

    public string Format(object response)
    {
        if (OutputMode != OutputModes.Plain && OutputMode != OutputModes.Hidden)
        {
            return JsonSerializer.Serialize(response, serializerOptions);
        }

        var elementType = response.GetType().IsGenericType ? response.GetType().GetGenericArguments()[0] : null;

        // Add special handling for enumerables to print each element on a new line
        // This won't necesarily work for lists of lists
        if (response is System.Collections.IEnumerable enumerable && response is not string)
        {
            // Use a separator object types for better readability
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
        if (OutputMode == OutputModes.Hidden)
        {
            outputSemaphore.Wait();
            try
            {
                Outputs.Add((StreamType.Stdout, output));
            }
            finally
            {
                outputSemaphore.Release();
            }
        }
        else
        {
            Console.WriteLine(output);
        }
    }

    public virtual void OutputError(object output)
    {
        OutputError(Format(output));
    }

    public virtual void OutputError(string output)
    {
        if (OutputMode == OutputModes.Hidden)
        {
            outputSemaphore.Wait();
            try
            {
                Outputs.Add((StreamType.Stderr, output));
            }
            finally
            {
                outputSemaphore.Release();
            }
        }
        else
        {
            outputSemaphore.Wait();
            try
            {
                var original = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;

                Console.Error.WriteLine(output);

                Console.ForegroundColor = original;
            }
            finally
            {
                outputSemaphore.Release();
            }
        }
    }

    public virtual void OutputConsole(string output)
    {
        if (OutputMode != OutputModes.Mcp && OutputMode != OutputModes.Hidden)
        {
            Console.Error.WriteLine(output);
        }
    }

    public virtual void OutputConsoleInfo(string output)
    {
        if (OutputMode != OutputModes.Mcp && OutputMode != OutputModes.Hidden)
        {
            Console.Error.WriteLine(output);
        }
    }

    public virtual void OutputConsoleWarning(string output)
    {
        if (OutputMode != OutputModes.Mcp && OutputMode != OutputModes.Hidden)
        {
            outputSemaphore.Wait();
            try
            {
                var original = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;

                Console.Error.WriteLine(output);

                Console.ForegroundColor = original;
            }
            finally
            {
                outputSemaphore.Release();
            }
        }
    }

    public virtual void OutputConsoleError(string output)
    {
        if (OutputMode != OutputModes.Mcp && OutputMode != OutputModes.Hidden)
        {
            outputSemaphore.Wait();
            try
            {
                var original = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;

                Console.Error.WriteLine(output);

                Console.ForegroundColor = original;
            }
            finally
            {
                outputSemaphore.Release();
            }
        }
    }

    public void OutputCommandResponse(CommandResponse output)
    {
        if (OutputMode != OutputModes.Mcp && output.ExitCode > 0)
        {
            OutputError(Format(output));
        }
        else
        {
            Output(Format(output));
        }
    }
}
