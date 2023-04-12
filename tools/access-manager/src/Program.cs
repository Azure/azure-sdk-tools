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

    var credential = new DefaultAzureCredential();
    var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    if (string.IsNullOrEmpty(githubToken))
    {
        Console.WriteLine("WARNING: GITHUB_TOKEN environment variable is not set. " +
                          "Operations will fail if githubRepositorySecrets is configured.");
    }
    var reconciler = new Reconciler(new GraphClient(credential), new RbacClient(credential), new GitHubClient(githubToken));
    await reconciler.Reconcile(accessConfig);
}
