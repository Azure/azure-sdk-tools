using System.CommandLine;
using System.Diagnostics;
using GitHubTeamUserStore.Constants;

namespace GitHubTeamUserStore
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var blobStorageURIOption = new Option<string>
                (name: "--blobStorageURI",
                description: "The blob storage URI including the SAS.");
            blobStorageURIOption.IsRequired = true;

            var rootCommand = new RootCommand
            {
                blobStorageURIOption,
            };
            rootCommand.SetHandler(PopulateTeamUserData,
                                   blobStorageURIOption);

            int returnCode = await rootCommand.InvokeAsync(args);
            Console.WriteLine($"Exiting with return code {returnCode}");
            Environment.Exit(returnCode);
        }

        private static async Task<int> PopulateTeamUserData(string blobStorageURI)
        {

            // Default the returnCode code to non-zero. If everything is successful it'll be set to 0
            int returnCode = 1;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            GitHubEventClient gitHubEventClient = new GitHubEventClient(ProductAndTeamConstants.ProductHeaderName, blobStorageURI);

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
