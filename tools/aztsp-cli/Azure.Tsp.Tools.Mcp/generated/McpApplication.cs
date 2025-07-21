namespace Mcp
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public record McpApplicationOptions
    {
        public McpTransport Transport { get; init; }
    }

    public enum McpTransport
    {
        Stdio,
        Sse,
    }

    public class McpApplication
    {
        public static IHost Create(McpApplicationOptions options)
        {
            if (options.Transport == McpTransport.Sse)
            {
                var builder = WebApplication.CreateBuilder();

                ConfigureServices(builder.Services, options);

                var application = builder.Build();

                application.MapMcp();
                application.Run("http://localhost:3001");

                return application;
            }
            else
            {
                var builder = Host.CreateApplicationBuilder();
                ConfigureServices(builder.Services, options);
                builder.Logging.AddConsole(consoleLogOptions =>
                {
                    // Configure all logs to go to stderr
                    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
                });

                return builder.Build();
            }
        }
        private static void ConfigureServices(IServiceCollection services, McpApplicationOptions options)
        {
            ConfigureMcpServer(services, options);
            Program.ConfigureServices(services);
        }
        public static void ConfigureMcpServer(IServiceCollection services, McpApplicationOptions options)
        {
            var mcp = services
                .AddMcpServer()
                .WithToolsFromAssembly();

            if (options.Transport == McpTransport.Sse)
            {
                mcp.WithHttpTransport();
            } else {
                mcp.WithStdioServerTransport();
            }
        }
    }
}