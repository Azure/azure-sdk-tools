using Mcp;
using Azure.Tsp.Tools.Mcp.Tools;

namespace Azure.Tsp.Tools.Mcp
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    public class Program
    {
        public static async Task Main(string[] args)
        {
            System.CommandLine.Option<McpTransport> transport = new("--transport")
            {
                Description = "What transport to use for the MCP server"
            };

            var rootCommand = new System.CommandLine.RootCommand("Sample app for System.CommandLine");
            rootCommand.Options.Add(transport);
            var parsed = rootCommand.Parse(args);
            var app = McpApplication.Create(
                new McpApplicationOptions
                {
                    Transport = parsed.GetValue(transport)
                }
            );
            await app.RunAsync();
        }
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IInit, InitImpl>();
        }
    }
}
