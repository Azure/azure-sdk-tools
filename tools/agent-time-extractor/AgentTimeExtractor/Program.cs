using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AgentTimeExtractor
{
    public class Program
    {
        private readonly Uri OrganizationUri = new Uri("https://dev.azure.com/azure-sdk");
        private readonly string[] ProjectNamesInScope = { "internal", "playground", "public" };
        private VssConnection connection;

        public async Task RunAsync(string personalAccessToken)
        {
            using (var writer = new StreamWriter("agenttimes.txt", false))
            {
                var credential = new VssBasicCredential("nobody", personalAccessToken);
                connection = new VssConnection(OrganizationUri, credential);

                var projectsInScope = await GetProjectsInScope();

                var buildClient = connection.GetClient<BuildHttpClient>();

                var allDefinitions = new List<BuildDefinitionReference>();

                foreach (var project in projectsInScope)
                {
                    Console.Write($"Project {project} has ");

                    var definitions = await buildClient.GetDefinitionsAsync(project.Id);
                    allDefinitions.AddRange(definitions);

                    Console.WriteLine($"{definitions.Count} definitions.");
                }

                var allBuilds = new List<Build>();

                foreach (var definition in allDefinitions)
                {
                    Console.Write($"Definition {definition.Project.Name}/{definition.Name} has ");

                    var builds = await buildClient.GetBuildsAsync(
                        project: definition.Project.Id,
                        definitions: new int[] { definition.Id },
                        minFinishTime: DateTime.Now.Subtract(TimeSpan.FromDays(21)),
                        statusFilter: BuildStatus.Completed,
                        top: 1000
                        );

                    allBuilds.AddRange(builds);

                    Console.WriteLine($"{builds.Count} builds."); 
                }

                foreach (var build in allBuilds)
                {
                    try
                    {
                        Console.Write($"Build {build.Project.Name}/{build.Definition.Name}/{build.Id} ran for ");
                        var timeline = await buildClient.GetBuildTimelineAsync(
                            project: build.Project.Id,
                            buildId: build.Id
                            );

                        var jobs = timeline.Records.Where(record => record.RecordType == "Job");
                        var agentTimeInSeconds = jobs.Select(job => job.FinishTime - job.StartTime).Sum(duration => duration.Value.TotalSeconds);

                        Console.WriteLine($"{agentTimeInSeconds} seconds.");

                        writer.WriteLine(
                            $"{build.Id}, {build.Project.Name}, {build.Definition.Name}, {build.StartTime}, {build.FinishTime}, {(build.FinishTime.Value - build.StartTime.Value).TotalSeconds}, {agentTimeInSeconds}, {build.Result}, {build.Reason}, {build.Repository.Id}, {build.RequestedBy.DisplayName}, {build.SourceBranch}"
                            );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"... skipped! {ex}");
                    }
                }
            }
        }

        private async Task<IEnumerable<TeamProjectReference>> GetProjectsInScope()
        {
            // Extract
            var projectClient = connection.GetClient<ProjectHttpClient>();
            var projects = await projectClient.GetProjects();
            var projectsInScope = projects.Where(project => ProjectNamesInScope.Contains(project.Name));
            return projectsInScope;
        }

        public async static Task Main(string[] args)
        {
            var personalAccessToken = Environment.GetEnvironmentVariable(args[0]);
            var program = new Program();
            await program.RunAsync(personalAccessToken);
        }
    }
}
