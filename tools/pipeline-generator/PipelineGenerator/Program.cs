using McMaster.Extensions.CommandLineUtils;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PipelineGenerator
{
    public class Program
    {

        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.HelpOption();
            var organizationOption = app.Option("--organization <url>", "The URL of the Azure DevOps organization.", CommandOptionType.SingleValue).IsRequired();
            var projectOption = app.Option("--project <project>", "The name of the Azure DevOps project.", CommandOptionType.SingleValue).IsRequired();
            var prefixOption = app.Option("--prefix <prefix>", "The prefix to append to the pipeline name.", CommandOptionType.SingleValue).IsRequired();
            var pathOption = app.Option("--path <path>", "The directory from which to scan for components", CommandOptionType.SingleValue).IsRequired();
            var patvarOption = app.Option("--patvar <env>", "Name of an environment variable which contains a PAT.", CommandOptionType.SingleValue).IsRequired();
            var conventionOption = app.Option("--convention <convention>", "The kind of pipelines we are creating.", CommandOptionType.SingleValue).IsRequired();
            var endpointOption = app.Option("--endpoint", "Name of the service endpoint to configure repositories with.", CommandOptionType.SingleValue).IsRequired();

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            app.OnExecute(() =>
            {
                var program = new Program();
                var exitCondition = program.RunAsync(
                    organizationOption.Value(),
                    projectOption.Value(),
                    prefixOption.Value(),
                    pathOption.Value(),
                    patvarOption.Value(),
                    conventionOption.Value(),
                    endpointOption.Value(),
                    cancellationTokenSource.Token
                    ).Result;

                return (int)exitCondition;
            });

            return app.Execute(args);
        }

        public async Task<ExitCondition> RunAsync(string organization, string project, string prefix, string path, string patvar, string convention, string endpoint, CancellationToken cancellationToken)
        {
            var scanDirectory = new DirectoryInfo(path);
            var scanner = new SdkComponentScanner();
            var components = scanner.Scan(scanDirectory);

            if (components.Count() == 0)
            {
                return ExitCondition.NoComponentsFound;
            }

            var connection = GetConnection(organization, patvar);
            var buildClient = await connection.GetClientAsync<BuildHttpClient>(cancellationToken);
            var projectClient = await connection.GetClientAsync<ProjectHttpClient>(cancellationToken);
            var serviceEndpointClient = await connection.GetClientAsync<ServiceEndpointHttpClient>(cancellationToken);

            var projects = await projectClient.GetProjects(ProjectState.WellFormed);
            var projectReference = projects.Single(p => p.Name.Equals(project, StringComparison.OrdinalIgnoreCase));

            foreach (var component in components)
            {
                var conventionDefinitionName = $"{prefix} - {component.Name} - {convention}";

                var matchingDefinitionReferences = await buildClient.GetDefinitionsAsync(
                    project: project,
                    name: conventionDefinitionName,
                    cancellationToken: cancellationToken
                    );

                // We should consider two definitions with the same name an error.
                var matchingDefinitionReference = matchingDefinitionReferences.SingleOrDefault();

                if (matchingDefinitionReference != null)
                {
                    var existingDefinition = await buildClient.GetDefinitionAsync(
                        projectReference.Id,
                        matchingDefinitionReference.Id,
                        cancellationToken: cancellationToken
                        );

                    var serviceEndpoints = await serviceEndpointClient.GetServiceEndpointsByNamesAsync(
                        projectReference.Id,
                        new string[] { "mitchdenny" },
                        cancellationToken: cancellationToken
                        );

                    var serviceEndpoint = serviceEndpoints.Single();

                    var repositories = await buildClient.ListRepositoriesAsync(
                        projectReference.Id,
                        serviceEndpoint.Data["pipelinesSourceProvider"],
                        serviceEndpointId: serviceEndpoint.Id,
                        repository: "azure/Azure-sdk-for-net",
                        cancellationToken: cancellationToken
                        );


                    Console.WriteLine($"FOUND (skipping for now): {component.Name}");
                }
                else
                {


                    //var repository = new BuildRepository()
                    //{
                    //    Id = "Azure/azure-sdk-for-net",
                    //    Url = new Uri("https://github.com/azure/azure-sdk-for-net/"),
                    //    DefaultBranch = "refs/heads/master",
                    //    Type = "GitHub"
                    //};

                    //var newDefinition = new BuildDefinition()
                    //{
                    //    Name = conventionDefinitionName,
                    //    Project = projectReference,
                    //    Repository = repository,
                    //    Process = new YamlProcess()
                    //    {
                    //        YamlFilename = "/sdk/servicebus/ci.yml"
                    //    }
                    //};

                    //await buildClient.CreateDefinitionAsync(
                    //    definition: newDefinition,
                    //    cancellationToken: cancellationToken
                    //    );
                }
            }

            return ExitCondition.Success;
        }

        private static VssConnection GetConnection(string organization, string patvar)
        {
            var pat = Environment.GetEnvironmentVariable(patvar);
            var credentials = new VssBasicCredential("nobody", pat);

            var connection = new VssConnection(new Uri(organization), credentials);
            return connection;
        }
    }
}
