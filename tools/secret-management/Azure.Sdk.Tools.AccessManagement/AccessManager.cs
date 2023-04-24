using Azure.Identity;

namespace Azure.Sdk.Tools.AccessManagement;

public class AccessManager
{
    public static async Task Run(List<string> configFiles)
    {
        var accessConfig = new AccessConfig(configFiles);

        var credential = new DefaultAzureCredential();
        var reconciler = new Reconciler(new GraphClient(credential), new RbacClient(credential), new GitHubClient());

        Console.WriteLine(accessConfig.ToString());
        Console.WriteLine("---");
        Console.WriteLine("Reconciling...");
        await reconciler.Reconcile(accessConfig);
    }
}