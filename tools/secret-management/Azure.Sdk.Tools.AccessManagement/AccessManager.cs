using Azure.Core;
using Azure.Identity;

namespace Azure.Sdk.Tools.AccessManagement;

public class AccessManager
{
    public static async Task Run(ILogger logger, List<string> configFiles)
    {
        AccessConfig accessConfig;
        TokenCredential credential;
        Reconciler reconciler;

        try
        {
            accessConfig = new AccessConfig(logger, configFiles);
            credential = new ChainedTokenCredential(new AzurePowerShellCredential(), new AzureCliCredential());
            reconciler = new Reconciler(logger, new GraphClient(logger, credential), new RbacClient(logger, credential), new GitHubClient(logger));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            throw;
        }

        logger.LogInformation(accessConfig.ToString());
        logger.LogInformation("---");
        logger.LogInformation("Reconciling...");
        await reconciler.Reconcile(accessConfig);
    }
}
