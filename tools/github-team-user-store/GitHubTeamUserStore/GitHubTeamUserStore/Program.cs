using System.CommandLine;
using System.Diagnostics;
using Azure.Storage.Blobs;
using GitHubTeamUserStore.Constants;

namespace GitHubTeamUserStore
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var teamUserBlobStorageUriOption = new Option<string>
                (name: "--teamUserBlobStorageURI", 
                description: "The team/user blob storage URI including the SAS.");
            teamUserBlobStorageUriOption.AddAlias("-tUri");
            teamUserBlobStorageUriOption.IsRequired = true;

            var userOrgVisibilityBlobStorageUriOption = new Option<string>
                (name: "--userOrgVisibilityBlobStorageURI",
                description: "The user/org blob storage URI including the SAS.");
            userOrgVisibilityBlobStorageUriOption.AddAlias("-uUri");
            userOrgVisibilityBlobStorageUriOption.IsRequired = true;

            var repoLabelBlobStorageUriOption = new Option<string>
                (name: "--repoLabelBlobStorageURI",
                description: "The repo/label blob storage URI including the SAS.");
            repoLabelBlobStorageUriOption.AddAlias("-rUri");
            repoLabelBlobStorageUriOption.IsRequired = true;

            // Since this will be running in the pipeline-owners-extraction, azure-sdk-tools
            // will be there. The command line option should be
            // --repositoryListFile "$(Build.SourcesDirectory)/tools/github/data/repositories.txt"
            var repositoryListFileOption = new Option<string>
                (name: "--repositoryListFile",
                description: "The data file which contains the list of repositorys to get labels for.");
            repositoryListFileOption.AddAlias("-rlFile");
            repositoryListFileOption.IsRequired = true;

            var rootCommand = new RootCommand
            {
                teamUserBlobStorageUriOption,
                userOrgVisibilityBlobStorageUriOption,
                repoLabelBlobStorageUriOption,
                repositoryListFileOption,
            };
            rootCommand.SetHandler(PopulateTeamUserData,
                                   teamUserBlobStorageUriOption,
                                   userOrgVisibilityBlobStorageUriOption,
                                   repoLabelBlobStorageUriOption,
                                   repositoryListFileOption);

            int returnCode = await rootCommand.InvokeAsync(args);
            Console.WriteLine($"Exiting with return code {returnCode}");
            Environment.Exit(returnCode);
        }

        private static async Task<int> PopulateTeamUserData(string teamUserBlobStorageUri,
                                                            string userOrgVisibilityBlobStorageUri,
                                                            string repoLabelBlobStorageUri, 
                                                            string repositoryListFile)
        {

            // Default the returnCode code to non-zero. If everything is successful it'll be set to 0
            int returnCode = 1;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            GitHubEventClient gitHubEventClient = new GitHubEventClient(ProductAndTeamConstants.ProductHeaderName);

            await gitHubEventClient.WriteRateLimits("RateLimit at start of execution:");
            bool success = false;
            // The team/user list needs to be generated before the user/org data. The reason being is that the User/Org
            // visibility data is generated for the azure-sdk-write team users.
            if (await TeamUserGenerator.GenerateAndStoreTeamUserAndOrgData(gitHubEventClient, teamUserBlobStorageUri, userOrgVisibilityBlobStorageUri))
            {
                if (await RepositoryLabelGenerator.GenerateAndStoreRepositoryLabels(gitHubEventClient, repoLabelBlobStorageUri, repositoryListFile))
                {
                    success = true;
                }
            }

            await gitHubEventClient.WriteRateLimits("RateLimit at end of execution:");

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine($"Total run time: {elapsedTime}");

            if (success)
            {
                Console.WriteLine("Data stored successfully.");
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
