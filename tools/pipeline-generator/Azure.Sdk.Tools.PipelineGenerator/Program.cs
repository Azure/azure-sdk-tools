using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PipelineGenerator.Conventions;
using PipelineGenerator.CommandParserOptions;

namespace PipelineGenerator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            await Parser.Default
                .ParseArguments<DefaultOptions, GenerateOptions>(args)
                .WithNotParsed(_ => { Environment.Exit((int)ExitCondition.InvalidArguments); })
                .WithParsedAsync(async o => { await Run(o, cancellationTokenSource); });
        }

        public static async Task Run(object commandObj, CancellationTokenSource cancellationTokenSource)
        {
            ExitCondition code = ExitCondition.Exception;

            switch (commandObj)
            {
                case GenerateOptions g:
                    var serviceProvider = GetServiceProvider(g.Debug);
                    var program = serviceProvider.GetService<Program>();
                    code = await program.RunAsync(
                        g.Organization,
                        g.Project,
                        g.Prefix,
                        g.Path,
                        g.Patvar,
                        g.Endpoint,
                        g.Repository,
                        g.Branch,
                        g.Agentpool,
                        g.Convention,
                        g.VariableGroups.ToArray(),
                        g.DevOpsPath,
                        g.WhatIf,
                        g.Open,
                        g.Destroy,
                        g.NoSchedule,
                        g.SetManagedVariables,
                        g.OverwriteTriggers,
                        cancellationTokenSource.Token
                    );

                    break;
                default:
                    code = ExitCondition.InvalidArguments;
                    break;
            }

            Environment.Exit((int)code);
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
            int[] variableGroups,
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
