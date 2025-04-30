
using System.Reflection;
using Azure.SDK.Tools.MCP.Contract;

namespace Azure.SDK.Tools.MCP.Hub
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            // todo: parse a command bundle here. pass it to CreateHostBuilder once we have an actual type
            // todo: can we have a "start" verb and a <tool> verb? EG if someone calls <server.exe> HelloWorld
            //   "This is a hello world input" -> we invoke just that tool
            //   "<server.exe> start" -> runs server responding to vscode copilot chat 
            CreateHostBuilder(args).Build().Run();
        }

        public static HostApplicationBuilder CreateHostBuilder(string[] args)
        {
            // todo: implement our own module discovery that takes the `--tools` or `--tools-exclude` when booting
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
            });
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                // todo: we can definitely honor the --tools param here to filter down the provided tools
                // for now, lets just use WithtoolsFromAssembly to actually run this thing
                .WithToolsFromAssembly();

            return builder;
        }

    }
}
