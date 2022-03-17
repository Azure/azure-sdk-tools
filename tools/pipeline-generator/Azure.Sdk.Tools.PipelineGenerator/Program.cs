﻿using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PipelineGenerator.Conventions;
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

        private static IServiceProvider GetServiceProvider(bool debug)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(config => config.AddConsole().SetMinimumLevel(debug ? LogLevel.Debug : LogLevel.Information))
                .AddTransient<Program>()
                .AddTransient<SdkComponentScanner>()
                .AddTransient<PullRequestValidationPipelineConvention>()
                .AddTransient<IntegrationTestingPipelineConvention>();

            return serviceCollection.BuildServiceProvider();
        }

        private static CommandLineApplication PrepareApplication(CancellationTokenSource cancellationTokenSource)
        {
            var app = new CommandLineApplication();
            app.HelpOption();
            var organizationOption = app.Option("--organization <url>", "The URL of the Azure DevOps organization.", CommandOptionType.SingleValue).IsRequired();
            var projectOption = app.Option("--project <project>", "The name of the Azure DevOps project.", CommandOptionType.SingleValue).IsRequired();
            var prefixOption = app.Option("--prefix <prefix>", "The prefix to append to the pipeline name.", CommandOptionType.SingleValue).IsRequired();
            var pathOption = app.Option("--path <path>", "The directory from which to scan for components", CommandOptionType.SingleValue).IsRequired();
            var devOpsPathOption = app.Option("--devopspath <path>", "The DevOps directory for created pipelines", CommandOptionType.SingleValue);
            var patvarOption = app.Option("--patvar <env>", "Name of an environment variable which contains a PAT.", CommandOptionType.SingleValue);
            var endpointOption = app.Option("--endpoint <endpoint>", "Name of the service endpoint to configure repositories with.", CommandOptionType.SingleValue).IsRequired();
            var repositoryOption = app.Option("--repository <repository>", "Name of the GitHub repo in the form [org]/[repo].", CommandOptionType.SingleValue).IsRequired();
            var branchOption = app.Option("--branch <branch>", "Typically refs/heads/main.", CommandOptionType.SingleValue).IsRequired();
            var agentpoolOption = app.Option("--agentpool <agentpool>", "Name of the agent pool to use when pool isn't specified.", CommandOptionType.SingleValue).IsRequired();
            var conventionOption = app.Option("--convention <convention>", "What convention are you building pipelines for?", CommandOptionType.SingleValue).IsRequired();
            var variablegroupsOption = app.Option("--variablegroup <variablegroup>", "Variable groups. May specify multiple (e.g. --variablegroup 1 --variablegroup 2)", CommandOptionType.MultipleValue);
            var whatifOption = app.Option("--whatif", "Use this to understand what will happen, but don't change anything.", CommandOptionType.NoValue);
            var openOption = app.Option("--open", "Open a browser window to the definitions that are created.", CommandOptionType.NoValue);
            var destroyOption = app.Option("--destroy", "Use this switch to delete the pipelines instead (DANGER!)", CommandOptionType.NoValue);
            var debugOption = app.Option("--debug", "Turn on debug level logging.", CommandOptionType.NoValue);
            var noScheduleOption = app.Option("--no-schedule", "Don't create any scheduled triggers.", CommandOptionType.NoValue);
            var setManagedVariables = app.Option("--set-managed-variables", "Set managed meta.* variable values", CommandOptionType.NoValue);
            var overwriteTriggersOption = app.Option("--overwrite-triggers", "Overwrite existing pipeline triggers (triggers may be manually modified, use with caution).", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                var serviceProvider = GetServiceProvider(debugOption.HasValue());
                var program = serviceProvider.GetService<Program>();
                var exitCondition = program.RunAsync(
                    organizationOption.Value(),
                    projectOption.Value(),
                    prefixOption.Value(),
                    pathOption.Value(),
                    patvarOption.Value(),
                    endpointOption.Value(),
                    repositoryOption.Value(),
                    branchOption.Value(),
                    agentpoolOption.Value(),
                    conventionOption.Value(),
                    variablegroupsOption.Values.ToArray(),
                    devOpsPathOption.Value(),
                    whatifOption.HasValue(),
                    openOption.HasValue(),
                    destroyOption.HasValue(),
                    noScheduleOption.HasValue(),
                    setManagedVariables.HasValue(),
                    overwriteTriggersOption.HasValue(),
                    cancellationTokenSource.Token
                    ).Result;

                return (int)exitCondition;
            });

            return app;
        }

        public Program(IServiceProvider serviceProvider, ILogger<Program> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        private IServiceProvider serviceProvider;
        private ILogger<Program> logger;

        public ILoggerFactory LoggerFactory { get; }

        private PipelineConvention GetPipelineConvention(string convention, PipelineGenerationContext context)
        {
            var normalizedConvention = convention.ToLower();

            switch (normalizedConvention)
            {
                case "ci":
                    var ciLogger = serviceProvider.GetService<ILogger<PullRequestValidationPipelineConvention>>();
                    return new PullRequestValidationPipelineConvention(ciLogger, context);

                case "up":
                    var upLogger = serviceProvider.GetService<ILogger<UnifiedPipelineConvention>>();
                    return new UnifiedPipelineConvention(upLogger, context);

                case "upweekly":
                    var upWeeklyTestLogger = serviceProvider.GetService<ILogger<WeeklyUnifiedPipelineConvention>>();
                    return new WeeklyUnifiedPipelineConvention(upWeeklyTestLogger, context);

                case "tests":
                    var testLogger = serviceProvider.GetService<ILogger<IntegrationTestingPipelineConvention>>();
                    return new IntegrationTestingPipelineConvention(testLogger, context);

                case "testsweekly":
                    var weeklyTestLogger = serviceProvider.GetService<ILogger<WeeklyIntegrationTestingPipelineConvention>>();
                    return new WeeklyIntegrationTestingPipelineConvention(weeklyTestLogger, context);

                default: throw new ArgumentOutOfRangeException(nameof(convention), "Could not find matching convention.");
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
            string branch,
            string agentPool,
            string convention,
            string[] variableGroups,
            string devOpsPath,
            bool whatIf,
            bool open,
            bool destroy,
            bool noSchedule,
            bool setManagedVariables,
            bool overwriteTriggers,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug("Creating context.");

                // Fall back to a form of prefix if DevOps path is not specified
                var devOpsPathValue = string.IsNullOrEmpty(devOpsPath) ? $"\\{prefix}" : devOpsPath;

                var context = new PipelineGenerationContext(
                    this.logger,
                    organization,
                    project,
                    patvar,
                    endpoint,
                    repository,
                    branch,
                    agentPool,
                    variableGroups,
                    devOpsPathValue,
                    prefix,
                    whatIf,
                    noSchedule,
                    setManagedVariables,
                    overwriteTriggers
                    );

                var pipelineConvention = GetPipelineConvention(convention, context);
                var components = ScanForComponents(path, pipelineConvention.SearchPattern);

                if (components.Count() == 0)
                {
                    logger.LogWarning("No components were found.");
                    return ExitCondition.NoComponentsFound;
                }

                logger.LogInformation("Found {0} components", components.Count());

                if (HasPipelineDefinitionNameDuplicates(pipelineConvention, components))
                {
                    return ExitCondition.DuplicateComponentsFound;
                }

                foreach (var component in components)
                {
                    logger.LogInformation("Processing component '{0}' in '{1}'.", component.Name, component.Path);
                    if (destroy)
                    {
                        var definition = await pipelineConvention.DeleteDefinitionAsync(component, cancellationToken);
                    }
                    else
                    {
                        var definition = await pipelineConvention.CreateOrUpdateDefinitionAsync(component, cancellationToken);

                        if (open)
                        {
                            OpenBrowser(definition.GetWebUrl());
                        }
                    }
                }

                return ExitCondition.Success;

            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "BOOM! Something went wrong, try running with --debug.");
                return ExitCondition.Exception;
            }
        }

        private void OpenBrowser(string url)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }


            logger.LogDebug("Launching browser window for: {0}", url);

            var processStartInfo = new ProcessStartInfo()
            {
                FileName = url,
                UseShellExecute = true,
            };

            // TODO: Need to test this on macOS and Linux.
            System.Diagnostics.Process.Start(processStartInfo);
        }

        private IEnumerable<SdkComponent> ScanForComponents(string path, string searchPattern)
        {
            var scanner = serviceProvider.GetService<SdkComponentScanner>();

            var scanDirectory = new DirectoryInfo(path);
            var components = scanner.Scan(scanDirectory, searchPattern);
            return components;
        }

        private bool HasPipelineDefinitionNameDuplicates(PipelineConvention convention, IEnumerable<SdkComponent> components)
        {
            var pipelineNames = new Dictionary<string, SdkComponent>();
            var duplicates = new HashSet<SdkComponent>();

            foreach (var component in components)
            {
                var definitionName = convention.GetDefinitionName(component);
                if (pipelineNames.TryGetValue(definitionName, out var duplicate))
                {
                    duplicates.Add(duplicate);
                    duplicates.Add(component);
                }
                else
                {
                    pipelineNames.Add(definitionName, component);
                }
            }

            if (duplicates.Count > 0) {
                logger.LogError("Found multiple pipeline definitions that will result in name collisions. This can happen when nested directory names are the same.");
                logger.LogError("Suggested fix: add a 'variant' to the yaml filename, e.g. 'sdk/keyvault/internal/ci.yml' => 'sdk/keyvault/internal/ci.keyvault.yml'");
                var paths = duplicates.Select(d => $"'{d.RelativeYamlPath}'");
                logger.LogError($"Pipeline definitions affected: {String.Join(", ", paths)}");

                return true;
            }

            return false;
        }
    }
}
