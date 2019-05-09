using McMaster.Extensions.CommandLineUtils;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var endpointOption = app.Option("--endpoint <endpoint>", "Name of the service endpoint to configure repositories with.", CommandOptionType.SingleValue).IsRequired();
            var repositoryOption = app.Option("--repository <repository>", "Name of the GitHub repo in the form [org]/[repo].", CommandOptionType.SingleValue).IsRequired();
            var conventionOption = app.Option("--convention <convention>", "What convention are you building pipelines for?", CommandOptionType.SingleValue).IsRequired();
            var variablegroupsOption = app.Option("--variablegroups <variablegroup>", "Comma seperated list of variable groups.", CommandOptionType.MultipleValue);
            var whatifOption = app.Option("--whatif", "Use this to understand what will happen, but don't change anything.", CommandOptionType.NoValue);
            var openOption = app.Option("--open", "Open a browser window to the definitions that are created.", CommandOptionType.NoValue);
            var destroyOption = app.Option("--destroy", "Use this switch to delete the pipelines instead (DANGER!)", CommandOptionType.NoValue);

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
                    endpointOption.Value(),
                    repositoryOption.Value(),
                    conventionOption.Value(),
                    variablegroupsOption.Values.ToArray(),
                    whatifOption.HasValue(),
                    openOption.HasValue(),
                    destroyOption.HasValue(),
                    cancellationTokenSource.Token
                    ).Result;

                return (int)exitCondition;
            });

            return app.Execute(args);
        }

        public Program()
        {

        }

        private Dictionary<string, Type> conventions = new Dictionary<string, Type>()
        {
            {"ci", typeof(PullRequestValidationPipelineConvention) },
            {"tests", typeof(IntegrationTestingPipelineConvention) }
        };

        private PipelineConvention GetPipelineConvention(string convention, PipelineGenerationContext context)
        {
            var normalizedConvention = convention.ToLower();
            if (conventions.ContainsKey(normalizedConvention))
            {
                return (PipelineConvention)Activator.CreateInstance(conventions[normalizedConvention], context);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(convention), "Could not find matching convention.");
            }
        }

        public async Task<ExitCondition> RunAsync(
            string organization,
            string project,
            string prefix,
            string path,
            string patvar,
            string endpoint,
            string repository,
            string convention,
            string[] variableGroups,
            bool whatIf,
            bool open,
            bool destroy,
            CancellationToken cancellationToken)
        {
            var context = new PipelineGenerationContext(
                organization,
                project,
                patvar,
                endpoint,
                repository,
                variableGroups
                );

            var pipelineConvention = GetPipelineConvention(convention, context);
            var components = ScanForComponents(path, pipelineConvention.SearchPattern);

            if (components.Count() == 0)
            {
                return ExitCondition.NoComponentsFound;
            }

            // Now we just iterate over each of the components and check to see whether
            // we have an existing build definition for it.
            foreach (var component in components)
            {
                var conventionDefinitionName = $"{prefix} - {component.Name} - ci";

                var matchingDefinitionReferences = await buildClient.GetDefinitionsAsync(
                    project: project,
                    name: conventionDefinitionName,
                    cancellationToken: cancellationToken
                    );

                // We should consider two definitions with the same name an error.
                var matchingDefinitionReference = matchingDefinitionReferences.SingleOrDefault();

                if (!destroy && matchingDefinitionReference != null)
                {
                    var referenceLink = (ReferenceLink)matchingDefinitionReference.Links.Links["web"];
                    Console.WriteLine($"Skipping: {component.Name} ({referenceLink.Href})");
                }
                else if (destroy && matchingDefinitionReference != null)
                {
                    var referenceLink = (ReferenceLink)matchingDefinitionReference.Links.Links["web"];
                    Console.WriteLine($"Destroying: {component.Name} ({referenceLink.Href})");

                    if (!whatIf)
                    {
                        await buildClient.DeleteDefinitionAsync(projectReference.Id, matchingDefinitionReference.Id, cancellationToken);
                    }
                }
                else if (!destroy && matchingDefinitionReference == null)
                {
                    Console.Write($"Creating: {component.Name}");

                    if (whatIf)
                    {
                        Console.WriteLine(" (whatif)");
                    }
                    else
                    {
                        var createdDefinition = await CreateDefinitionAsync(projectReference, buildClient, sourceRepository, conventionDefinitionName, component, cancellationToken);

                        var referenceLink = (ReferenceLink)createdDefinition.Links.Links["web"];
                        Console.WriteLine($" ({referenceLink.Href})");

                        if (open && Environment.OSVersion.Platform == PlatformID.Win32NT)
                        {
                            var processStartInfo = new ProcessStartInfo()
                            {
                                FileName = referenceLink.Href.ToString(),
                                UseShellExecute = true
                            };
                            // TODO: Need to test this on macOS and Linux.
                            System.Diagnostics.Process.Start(processStartInfo);
                        }
                    }
                }
            }

            return ExitCondition.Success;
        }

        private IEnumerable<SdkComponent> ScanForComponents(string path, string searchPattern)
        {
            var scanDirectory = new DirectoryInfo(path);
            var scanner = new SdkComponentScanner();
            var components = scanner.Scan(scanDirectory, searchPattern);
            return components;
        }

        private async Task<BuildDefinition> CreateDefinitionAsync(TeamProjectReference projectReference, BuildHttpClient buildClient, SourceRepository sourceRepository, string conventionDefinitionName, SdkComponent component, CancellationToken cancellationToken)
        {
            var repositoryHelper = new RepositoryHelper();
            var root = repositoryHelper.GetRepositoryRoot(component.Path);
            var relativePath = Path.GetRelativePath(root, component.Path.FullName);
            var yamlPath = Path.Combine(relativePath, "ci.yml");

            var buildRepository = new BuildRepository()
            {
                DefaultBranch = "refs/heads/master",
                Id = sourceRepository.Id,
                Name = sourceRepository.FullName,
                Type = "GitHub",
                Url = new Uri(sourceRepository.Properties["cloneUrl"]),
            };

            buildRepository.Properties.AddRangeIfRangeNotNull(sourceRepository.Properties);

            var newDefinition = new BuildDefinition()
            {
                Name = conventionDefinitionName,
                Project = projectReference,
                Repository = buildRepository,
                Process = new YamlProcess()
                {
                    YamlFilename = yamlPath
                },
                Queue = new AgentPoolQueue()
                {
                    Id = 42
                }
            };

            newDefinition.Triggers.Add(new ContinuousIntegrationTrigger()
            {
                SettingsSourceType = 2 // HACK: This is editor invisible, but this is required to inherit branch filters from YAML file.
            });

            newDefinition.Triggers.Add(new PullRequestTrigger()
            {
                SettingsSourceType = 2, // HACK: See above.
                Forks = new Forks()
                {
                    AllowSecrets = false,
                    Enabled = true
                }
            });

            var createdDefinition = await buildClient.CreateDefinitionAsync(
                definition: newDefinition,
                cancellationToken: cancellationToken
                );
            return createdDefinition;
        }

    }
}
