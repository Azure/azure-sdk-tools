// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Helpers;

// NOTE: the classes in this file are not thread-safe.
// While the MCP server is intended to run only once per process
// any concurrency changes in the future may require updates to this file.

public interface IMcpServerContextAccessor
{
    McpServer? Current { get; }
    bool IsEnabled { get; }
    void Initialize(McpServer? server);
    void Disable();
}

public sealed class McpServerContextAccessor : IMcpServerContextAccessor
{
    private McpServer? current;
    private bool enabled = true;

    public McpServer? Current
    {
        get => enabled ? current : null;
    }

    public bool IsEnabled => enabled;

    public void Initialize(McpServer? server)
    {
        if (!enabled || server == null || current != null)
        {
            return;
        }

        current = server;
    }

    public void Disable()
    {
        enabled = false;
        current = null;
    }
}

public sealed class McpServerLoggerProvider(IMcpServerContextAccessor contextAccessor) : ILoggerProvider
{
    private ILoggerProvider? provider;
    private ILogger? logger;

    public ILogger CreateLogger(string categoryName) => new McpServerLogger(this, categoryName);

    public void Dispose()
    {
        provider?.Dispose();
        provider = null;
        logger = null;
    }

    private ILogger? TryGetLogger(string categoryName)
    {
        if (!contextAccessor.IsEnabled || contextAccessor.Current == null)
        {
            return null;
        }

        provider ??= contextAccessor.Current.AsClientLoggerProvider();
        logger ??= provider.CreateLogger("azsdk");
        return logger;
    }

    private sealed class McpServerLogger(McpServerLoggerProvider owner, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return owner.TryGetLogger(categoryName)?.BeginScope(state) ?? NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return owner.TryGetLogger(categoryName)?.IsEnabled(logLevel) == true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var logger = owner.TryGetLogger(categoryName);
            if (logger == null)
            {
                return;
            }

            try
            {
                logger.Log(logLevel, eventId, state, exception, formatter);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

public sealed class McpRawOutputHelper(ILogger<McpRawOutputHelper> logger) : IRawOutputHelper
{
    public void OutputConsole(string output)
    {
        logger.LogInformation("{Output}", output);
    }

    public void OutputConsoleError(string output)
    {
        logger.LogError("{Output}", output);
    }
}
