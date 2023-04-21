using Azure.Identity;

namespace Azure.Sdk.Tools.AccessManagement;

public class AccessManager
{
    public static async Task Run(List<string> configFiles)
    {
        foreach (var file in configFiles)
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