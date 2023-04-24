using Azure.Identity;

namespace Azure.Sdk.Tools.AccessManagement;

public class AccessManager
{
    public static async Task Run(List<string> configFiles)
    {
        AccessConfig accessConfig;
        DefaultAzureCredential credential;
        Reconciler reconciler;

        try
        {
            accessConfig = new AccessConfig(configFiles);
            credential = new DefaultAzureCredential();
            reconciler = new Reconciler(new GraphClient(credential), new RbacClient(credential), new GitHubClient());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }


        Console.WriteLine(accessConfig.ToString());
        Console.WriteLine("---");
        Console.WriteLine("Reconciling...");
        await reconciler.Reconcile(accessConfig);
    }
}