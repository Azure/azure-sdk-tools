using System.Text;
using Azure.SDK.Tools.MCP.Hub.Services.Azure;
using Azure.SDK.Tools.MCP.Hub.Tools.AzurePipelinesTool;

namespace Azure.SDK.Tools.MCP.Hub;

public sealed class Program
{
    public static void Main(string[] args)
    {
        // var task = TestAzp();
        var task = TestAI();
        task.Wait();
        return;
        // todo: parse a command bundle here. pass it to CreateHostBuilder once we have an actual type
        // todo: can we have a "start" verb and a <tool> verb? EG if someone calls <server.exe> HelloWorld
        //   "This is a hello world input" -> we invoke just that tool
        //   "<server.exe> start" -> runs server responding to vscode copilot chat
        var host = CreateAppBuilder(args).Build();
        // For testing SSE can be easier to use. Comment above and uncomment below. Eventually this will be
        // behind a command line flag or we could try to run in both modes at once if possible.
        host.MapMcp();
        host.Run();
    }

    public static WebApplicationBuilder CreateAppBuilder(string[] args)
    {
        // todo: implement our own module discovery that takes the `--tools` or `--tools-exclude` when booting
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
        });

        builder.Services.AddSingleton<IAzureService, AzureService>();

        builder.Services
            .AddMcpServer()
            //.WithStdioServerTransport()
            // For testing SSE can be easier to use. Comment above and uncomment below. Eventually this will be
            // behind a command line flag or we could try to run in both modes at once if possible.
            .WithHttpTransport()
            // todo: we can definitely honor the --tools param here to filter down the provided tools
            // for now, lets just use WithtoolsFromAssembly to actually run this thing
            .WithToolsFromAssembly();

        return builder;
    }

    public static async Task TestAzp()
    {
        var azureService = new AzureService();
        var searchService = new SearchService(azureService);
        var azpTool = new AzurePipelinesTool(azureService, searchService)
        {
            project = "public"
        };

        Console.WriteLine("Testing Azure Pipelines Tool...");
        var output = await azpTool.AnalyzePipelineFailureLog(4815910, 174);
        Console.WriteLine(output);
    }

    public static async Task TestAI()
    {
        var azureService = new AzureService();
        var ai = new AIAgentService(azureService);

        Console.WriteLine("Testing AI Agents Service...");
        await ai.UploadFileAsync(new MemoryStream(Encoding.UTF8.GetBytes("Test file content")), "test.txt");
    }
}