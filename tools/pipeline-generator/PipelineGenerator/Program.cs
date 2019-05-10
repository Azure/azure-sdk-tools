using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            var app = PrepareApplication(cancellationTokenSource);
            return app.Execute(args);
        }

        private static CommandLineApplication PrepareApplication(CancellationTokenSource cancellationTokenSource)
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
            var agentpoolOption = app.Option("--agentpool <agentpool>", "Name of the agent pool to use when pool isn't speciifed.", CommandOptionType.SingleValue).IsRequired();
            var conventionOption = app.Option("--convention <convention>", "What convention are you building pipelines for?", CommandOptionType.SingleValue).IsRequired();
            var variablegroupsOption = app.Option("--variablegroups <variablegroup>", "Comma seperated list of variable groups.", CommandOptionType.MultipleValue);
            var whatifOption = app.Option("--whatif", "Use this to understand what will happen, but don't change anything.", CommandOptionType.NoValue);
            var openOption = app.Option("--open", "Open a browser window to the definitions that are created.", CommandOptionType.NoValue);
            var destroyOption = app.Option("--destroy", "Use this switch to delete the pipelines instead (DANGER!)", CommandOptionType.NoValue);

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
                    agentpoolOption.Value(),
                    conventionOption.Value(),
                    variablegroupsOption.Values.ToArray(),
                    whatifOption.HasValue(),
                    openOption.HasValue(),
                    destroyOption.HasValue(),
                    cancellationTokenSource.Token
                    ).Result;

                return (int)exitCondition;
            });

            return app;
        }

        public Program()
        {
        }

        private Dictionary<string, Type> conventions = new Dictionary<string, Type>()
        {
            {"ci", typeof(PullRequestValidationPipelineConvention) },
            {"tests", typeof(IntegrationTestingPipelineConvention) }
        };

        public ILoggerFactory LoggerFactory { get; }

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
            string agentPool,
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
                agentPool,
                variableGroups,
                prefix,
                whatIf
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
                if (destroy)
                {
                    var definition = await pipelineConvention.DeleteDefinitionAsync(component, cancellationToken);
                }
                else
                {
                    var definition = await pipelineConvention.CreateOrUpdateDefinitionAsync(component, cancellationToken);

                    if (open)
                    {
                        if (open && Environment.OSVersion.Platform == PlatformID.Win32NT)
                        {
                            var referenceLink = (ReferenceLink)definition.Links.Links["web"];
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
    }
}
