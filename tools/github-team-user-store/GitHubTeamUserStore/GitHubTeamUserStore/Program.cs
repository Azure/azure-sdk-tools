using System.CommandLine;
using System.Diagnostics;
using GitHubTeamUserStore.Constants;

namespace GitHubTeamUserStore
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var outputDirectoryOption = new Option<string>
                (name: "--outputDirectory",
                description: "The directory where cache files will be written.");
            outputDirectoryOption.AddAlias("-oDir");
            outputDirectoryOption.IsRequired = true;

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
                outputDirectoryOption,
                repositoryListFileOption,
            };
            rootCommand.SetHandler(GenerateCacheFiles,
                                   outputDirectoryOption,
                                   repositoryListFileOption);

            int returnCode = await rootCommand.InvokeAsync(args);
            Console.WriteLine($"Exiting with return code {returnCode}");
            Environment.Exit(returnCode);
        }

        private static async Task<int> GenerateCacheFiles(string outputDirectory,
                                                          string repositoryListFile)
        {
            // Default the returnCode code to non-zero. If everything is successful it'll be set to 0
            int returnCode = 1;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            string fullOutputDirectory = PrepareOutputDirectory(outputDirectory);
            string teamUserOutputPath = Path.Combine(fullOutputDirectory, ProductAndTeamConstants.TeamUserCacheFileName);
            string userOrgVisibilityOutputPath = Path.Combine(fullOutputDirectory, ProductAndTeamConstants.UserOrgVisibilityCacheFileName);
            string repoLabelOutputPath = Path.Combine(fullOutputDirectory, ProductAndTeamConstants.RepositoryLabelCacheFileName);

            OpenSourceApiClient openSourceApiClient = new OpenSourceApiClient();

            bool success = false;
            // The team/user list needs to be generated before the user/org data. The reason being is that the User/Org
            // visibility data is generated for the azure-sdk-write team users.
            if (await TeamUserGenerator.GenerateAndWriteTeamUserAndOrgData(openSourceApiClient, teamUserOutputPath, userOrgVisibilityOutputPath))
            {
                if (await RepositoryLabelGenerator.GenerateAndWriteRepositoryLabels(openSourceApiClient, repoLabelOutputPath, repositoryListFile))
                {
                    success = true;
                }
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine($"Total run time: {elapsedTime}");

            if (success)
            {
                Console.WriteLine($"Cache files generated successfully in {fullOutputDirectory}.");
                returnCode = 0;
            }
            else
            {
                Console.WriteLine("There were issues with generated cache files. See above for specifics.");
            }
            return returnCode;
        }

        private static string PrepareOutputDirectory(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("The output directory cannot be null or whitespace.", nameof(outputDirectory));
            }

            string fullOutputDirectory = Path.GetFullPath(outputDirectory);
            if (File.Exists(fullOutputDirectory))
            {
                throw new ArgumentException($"The output directory path '{outputDirectory}' points to a file.", nameof(outputDirectory));
            }

            Directory.CreateDirectory(fullOutputDirectory);
            return fullOutputDirectory;
        }
    }
}
