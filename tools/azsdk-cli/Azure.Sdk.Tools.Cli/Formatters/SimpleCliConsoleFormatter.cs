// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.Cli.Formatters;

/// <summary>
/// A console log formatter for CLI mode that outputs just the log message
/// with ANSI color coding:
///   - Info: default color
///   - Warning: yellow
///   - Error/Critical: red
///
/// In debug mode, the built-in SimpleConsole formatter is used instead.
/// </summary>
public sealed class SimpleCliConsoleFormatter : ConsoleFormatter, IDisposable
{
    public const string FormatterName = "simple-cli";

    private const string AnsiReset = "\x1b[0m";
    private const string AnsiYellow = "\x1b[33m";
    private const string AnsiRed = "\x1b[31m";

    private readonly IDisposable? _optionsReloadToken;
    private readonly bool _useColor;
    private ConsoleFormatterOptions _options;

    public SimpleCliConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options.CurrentValue;
        _optionsReloadToken = options.OnChange(updated => _options = updated);
        _useColor = !Console.IsErrorRedirected;
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null)
        {
            return;
        }

        var (colorStart, colorEnd) = _useColor ? logEntry.LogLevel switch
        {
            LogLevel.Warning => (AnsiYellow, AnsiReset),
            LogLevel.Error or LogLevel.Critical => (AnsiRed, AnsiReset),
            _ => ((string?)null, (string?)null),
        } : ((string?)null, (string?)null);

        if (colorStart is not null)
        {
            textWriter.Write(colorStart);
        }

        textWriter.WriteLine(message);

        if (logEntry.Exception is not null)
        {
            textWriter.WriteLine(logEntry.Exception.ToString());
        }

        if (colorEnd is not null)
        {
            textWriter.Write(colorEnd);
        }
    }

    public void Dispose()
    {
        _optionsReloadToken?.Dispose();
    }
}
