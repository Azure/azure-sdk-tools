
using System.Reflection;
using Azure.SDK.Tools.MCP.Contract;

namespace Azure.SDK.Tools.MCP.Hub
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            // parse CLI args to figure out workload

            // then collect unmapped args to pass on to ASP.NET?

            // create the hostbuilder and run if necessary
            CreateHostBuilder(args).Build().Run();
        }

        public static HostApplicationBuilder CreateHostBuilder(string[] args)
        {

            // call discoverModules, iterate across them and add to builder.Services
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
            });
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                // we can definitely honor the --tools param here to filter down the provided tools
                // for now, lets just use WithtoolsFromAssembly to actually run this thing
                // after we grep through the modules 
                .WithToolsFromAssembly();

            return builder;
        }

        // todo: implement our own module discovery that takes parameters into account
    }
}
