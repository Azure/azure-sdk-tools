using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.AccessManager;
using Azure.Identity;

namespace Azure.Sdk.Tools.SecretManagement.Cli.Commands;

public class SyncAccessCommand : Command
{
    private readonly Option<string[]> fileOption = new(new[] { "--file", "-f" })
    {
        Arity = ArgumentArity.OneOrMore,
        Description = "Name of the plan to sync.",
        IsRequired = true,
    };

    public SyncAccessCommand() : base("sync-access", "RBAC and Federated Identity manager for AAD applications")
    {
        AddOption(this.fileOption);
        this.SetHandler(Run);
    }

    public async Task Run(InvocationContext invocationContext)
    {
        var fileOptions = invocationContext.ParseResult.GetValueForOption(this.fileOption);
        foreach (var file in fileOptions?.ToList() ?? new List<string>())
        {
            var config = new FileInfo(file);
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
    }
}