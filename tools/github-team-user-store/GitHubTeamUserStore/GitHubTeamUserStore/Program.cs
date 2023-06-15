using System.Diagnostics;
using GitHubTeamUserStore.Constants;

namespace GitHubTeamUserStore
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            GitHubEventClient gitHubEventClient = new GitHubEventClient(ProductAndTeamConstants.ProductHeaderName);
            await gitHubEventClient.WriteRateLimits("RateLimit at start of execution:");
            await TeamUserGenerator.GenerateTeamUserList(gitHubEventClient);
            await gitHubEventClient.WriteRateLimits("RateLimit at end of execution:");
            bool storedEqualsGenerated = await TeamUserGenerator.VerifyStoredTeamUsers(gitHubEventClient);

            stopWatch.Stop();
            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopWatch.Elapsed;

            // Format and display the TimeSpan value.
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine($"Total run time: {elapsedTime}");

            if (storedEqualsGenerated)
            {
                Console.WriteLine("List stored successfully.");
            }
            else
            {
                Console.WriteLine("There were issues with generated vs stored data. See above for specifics.");
                Environment.Exit(1);
            }
        }
    }
}
