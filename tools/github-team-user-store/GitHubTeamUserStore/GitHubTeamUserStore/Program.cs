using System.CommandLine;
using System.Diagnostics;
using GitHubTeamUserStore.Constants;

namespace GitHubTeamUserStore
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var azureBlobAccountNameOption = new Option<string>
                (name: "--blobAccountName",
                description: "The name of the Azure blob account.",
                getDefaultValue: () => DefaultStorageConstants.DefaultAzureBlobAccountName);

            var azureSdkWriteTeamsContainerOption = new Option<string>
                (name: "--teamsContainer",
                description: "The name of the Azure SDK Write Teams Container.",
                getDefaultValue: () => DefaultStorageConstants.DefaultAzureSdkWriteTeamsContainer);

            var azureSdkWriteTeamsBlobNameOption = new Option<string>
                (name: "--teamsBlobName",
                description: "The name of the Azure SDK Write Teams Blob.",
                getDefaultValue: () => DefaultStorageConstants.DefaultAzureSdkWriteTeamsBlobName);

            var rootCommand = new RootCommand
            {
                azureBlobAccountNameOption,
                azureSdkWriteTeamsContainerOption,
                azureSdkWriteTeamsBlobNameOption
            };
            rootCommand.SetHandler(PopulateTeamUserData, 
                                   azureBlobAccountNameOption, 
                                   azureSdkWriteTeamsContainerOption, 
                                   azureSdkWriteTeamsBlobNameOption);

            int returnCode = await rootCommand.InvokeAsync(args);
            Console.WriteLine($"Exiting with return code {returnCode}");
            Environment.Exit(returnCode);
        }

        private static async Task<int> PopulateTeamUserData(string azureBlobAccountName,
                                                            string azureSdkWriteTeamsContainer,
                                                            string azureSdkWriteTeamsBlobName)
        {

            // Default the returnCode code to non-zero. If everything is successful it'll be set to 0
            int returnCode = 1;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            GitHubEventClient gitHubEventClient = new GitHubEventClient(ProductAndTeamConstants.ProductHeaderName);
            gitHubEventClient.AzureBlobAccountName = azureBlobAccountName;
            Console.WriteLine($"AzureBlobAccountName={gitHubEventClient.AzureBlobAccountName}");
            gitHubEventClient.AzureSdkWriteTeamsBlobName = azureSdkWriteTeamsBlobName;
            Console.WriteLine($"AzureSdkWriteTeamsBlobName={gitHubEventClient.AzureSdkWriteTeamsBlobName}");
            gitHubEventClient.AzureSdkWriteTeamsContainer = azureSdkWriteTeamsContainer;
            Console.WriteLine($"AzureSdkWriteTeamsContainer={gitHubEventClient.AzureSdkWriteTeamsContainer}");

            await gitHubEventClient.WriteRateLimits("RateLimit at start of execution:");
            await TeamUserGenerator.GenerateAndStoreTeamUserList(gitHubEventClient);
            await gitHubEventClient.WriteRateLimits("RateLimit at end of execution:");
            bool storedEqualsGenerated = await TeamUserGenerator.VerifyStoredTeamUsers(gitHubEventClient);

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine($"Total run time: {elapsedTime}");

            if (storedEqualsGenerated)
            {
                Console.WriteLine("List stored successfully.");
                returnCode = 0;
            }
            else
            {
                Console.WriteLine("There were issues with generated vs stored data. See above for specifics.");
            }
            return returnCode;
        }
    }
}
