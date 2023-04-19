using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.SecretManagement.Cli;

internal sealed class SimplerConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "simpler";

    private const string DefaultForegroundColor = "\x1B[39m\x1B[22m"; // reset to default foreground color
    private const string DefaultBackgroundColor = "\x1B[49m"; // reset to the background color

    public SimplerConsoleFormatter(IOptions<ConsoleFormatterOptions> options) : base(FormatterName)
    {
        FormatterOptions = options.Value;
    }

    internal ConsoleFormatterOptions FormatterOptions { get; set; }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        string? message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        if (logEntry.Exception == null && message == null)
        {
            return;
        }

        string? timestampFormat = FormatterOptions.TimestampFormat;

        if (timestampFormat != null)
        {
            DateTimeOffset currentDateTime = GetCurrentDateTime();
            string timestamp = currentDateTime.ToString(timestampFormat);
            textWriter.Write(timestamp);
        }

        LogLevel logLevel = logEntry.LogLevel;
        (ConsoleColor Foreground, ConsoleColor Background)? logLevelColors = GetLogLevelConsoleColors(logLevel);
        string? logLevelString = GetLogLevelString(logLevel);

        if (logLevelString != null)
        {
            WriteColoredMessage(textWriter, logLevelString, logLevelColors?.Background, logLevelColors?.Foreground);
        }

        CreateDefaultLogMessage(textWriter, logEntry, message, scopeProvider);
    }

    private static void CreateDefaultLogMessage<TState>(TextWriter textWriter, in LogEntry<TState> logEntry,
        string message, IExternalScopeProvider? scopeProvider)
    {
        // Example:
        // info: ConsoleApp.Program[10]
        //       Request received
        // scope information
        textWriter.Write(message.TrimEnd());

        // Example:
        // System.InvalidOperationException
        //    at Namespace.Class.Function() in File:line X
        Exception? exception = logEntry.Exception;
        if (exception != null)
        {
            // exception message
            textWriter.Write(' ');
            textWriter.Write(exception.ToString());
        }

        textWriter.Write(Environment.NewLine);
    }


    private DateTimeOffset GetCurrentDateTime()
    {
        return FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
    }

    private static string? GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "trce: ",
            LogLevel.Debug => "dbug: ",
            LogLevel.Information => null,
            LogLevel.Warning => "warn: ",
            LogLevel.Error => "fail: ",
            LogLevel.Critical => "crit: ",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };
    }

    private static void WriteColoredMessage(TextWriter textWriter, string message, ConsoleColor? background,
        ConsoleColor? foreground)
    {
        // Order: backgroundcolor, foregroundcolor, Message, reset foregroundcolor, reset backgroundcolor
        if (background.HasValue)
        {
            textWriter.Write(GetBackgroundColorEscapeCode(background.Value));
        }

        if (foreground.HasValue)
        {
            textWriter.Write(GetForegroundColorEscapeCode(foreground.Value));
        }

        textWriter.Write(message);
        if (foreground.HasValue)
        {
            textWriter.Write(DefaultForegroundColor); // reset to default foreground color
        }

        if (background.HasValue)
        {
            textWriter.Write(DefaultBackgroundColor); // reset to the background color
        }
    }

    private static string GetForegroundColorEscapeCode(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "\x1B[30m",
            ConsoleColor.DarkRed => "\x1B[31m",
            ConsoleColor.DarkGreen => "\x1B[32m",
            ConsoleColor.DarkYellow => "\x1B[33m",
            ConsoleColor.DarkBlue => "\x1B[34m",
            ConsoleColor.DarkMagenta => "\x1B[35m",
            ConsoleColor.DarkCyan => "\x1B[36m",
            ConsoleColor.Gray => "\x1B[37m",
            ConsoleColor.Red => "\x1B[1m\x1B[31m",
            ConsoleColor.Green => "\x1B[1m\x1B[32m",
            ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
            ConsoleColor.Blue => "\x1B[1m\x1B[34m",
            ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
            ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
            ConsoleColor.White => "\x1B[1m\x1B[37m",
            _ => DefaultForegroundColor // default foreground color
        };
    }

    private static string GetBackgroundColorEscapeCode(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => "\x1B[40m",
            ConsoleColor.DarkRed => "\x1B[41m",
            ConsoleColor.DarkGreen => "\x1B[42m",
            ConsoleColor.DarkYellow => "\x1B[43m",
            ConsoleColor.DarkBlue => "\x1B[44m",
            ConsoleColor.DarkMagenta => "\x1B[45m",
            ConsoleColor.DarkCyan => "\x1B[46m",
            ConsoleColor.Gray => "\x1B[47m",
            _ => DefaultBackgroundColor // Use default background color
        };
    }

    private static (ConsoleColor Foreground, ConsoleColor Background)? GetLogLevelConsoleColors(LogLevel logLevel)
    {
        // We must explicitly set the background color if we are setting the foreground color,
        // since just setting one can look bad on the users console.
        return logLevel switch
        {
            LogLevel.Trace => (ConsoleColor.Blue, ConsoleColor.Black),
            LogLevel.Debug => (ConsoleColor.Blue, ConsoleColor.Black),
            LogLevel.Information => (ConsoleColor.DarkGreen, ConsoleColor.Black),
            LogLevel.Warning => (ConsoleColor.Yellow, ConsoleColor.Black),
            LogLevel.Error => (ConsoleColor.Black, ConsoleColor.DarkRed),
            LogLevel.Critical => (ConsoleColor.White, ConsoleColor.DarkRed),
            _ => null
        };
    }
}
