using System.CommandLine;
using Azure.Identity;

await Entrypoint(args);

static async Task Entrypoint(string[] args)
{
    var rootCommand = new RootCommand("RBAC and Federated Identity manager for Azure SDK apps");
    var fileArgument = new Argument<FileInfo>("file", "Path to access config file for identities");

    rootCommand.Add(fileArgument);
    rootCommand.SetHandler(async (fileArgumentValue) => await Run(fileArgumentValue), fileArgument);

    await rootCommand.InvokeAsync(args);
}

static async Task Run(FileInfo config)
{
    Console.WriteLine("Using config -> " + config.FullName + Environment.NewLine);

    var accessConfig = new AccessConfig(config.FullName);
    Console.WriteLine(accessConfig.ToString());
    Console.WriteLine("---");
    Console.WriteLine("Reconciling...");

    var credential = new DefaultAzureCredential();
    var reconciler = new Reconciler(new GraphClient(credential), new RbacClient(credential), new GitHubClient());

    try
    {
        await reconciler.Reconcile(accessConfig);
    }
    catch
    {
        Environment.Exit(1);
    }
}