using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Text;
using Azure.Sdk.Tools.SecretManagement.Cli.Commands;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;

namespace Azure.Sdk.Tools.SecretManagement.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Secrets rotation tool");
        rootCommand.AddCommand(new ListCommand());
        rootCommand.AddCommand(new StatusCommand());
        rootCommand.AddCommand(new RotateCommand());

        CommandLineBuilder cliBuilder = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseExceptionHandler(HandleExceptions);

        return await cliBuilder.Build().InvokeAsync(args);
    }

    private static void HandleExceptions(Exception exception, InvocationContext invocationContext)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(BuildErrorMessage(exception).TrimEnd());
        Console.ResetColor();
        invocationContext.ExitCode = 1;
    }

    private static string BuildErrorMessage(Exception exception)
    {
        var builder = new StringBuilder();

        if (exception is RotationConfigurationException)
        {
            builder.AppendLine("Configuration error:");
            AppendKnownExceptionMessage(builder, exception);
        }
        else if (exception is RotationException)
        {
            builder.AppendLine("Rotation error:");
            AppendKnownExceptionMessage(builder, exception);
        }
        else if (exception is RotationCliException)
        {
            builder.AppendLine("Command invocation error:");
            AppendKnownExceptionMessage(builder, exception);
        }
        else
        {
            AppendUnknownExceptionMessage(builder, exception);
        }

        return builder.ToString();
    }

    private static void AppendKnownExceptionMessage(StringBuilder builder, Exception exception)
    {
        builder.Append("  ");
        builder.AppendJoin("\n  ", exception.Message.Split("\n"));

        if (exception.InnerException != null)
        {
            builder.Append("\n    ");
            builder.AppendJoin("\n    ", exception.InnerException.Message.Split("\n"));
        }
    }

    private static void AppendUnknownExceptionMessage(StringBuilder builder, Exception exception, int indent = 0,
        bool isInnerException = false)
    {
        string indentString = new string(' ', indent);
        string prefix = isInnerException ? "----> " : "";
        string[] messageStrings = $"{prefix}{exception.GetType().Name}: {exception.Message}".Split('\n');

        foreach (string messageString in messageStrings)
        {
            builder.Append(indentString);
            builder.AppendLine(messageString);
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (Exception innerException in aggregateException.InnerExceptions)
            {
                AppendUnknownExceptionMessage(builder, innerException, indent + 2, true);
            }
        }
        else if (exception.InnerException != null)
        {
            AppendUnknownExceptionMessage(builder, exception.InnerException, indent + 2, true);
        }
    }
}
