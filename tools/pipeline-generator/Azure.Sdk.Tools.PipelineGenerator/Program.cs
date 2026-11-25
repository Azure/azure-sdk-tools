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
using Microsoft.TeamFoundation.Build.WebApi;
using System.Text.Json.Nodes;
using System.Text.Json;

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
                .ParseArguments<DefaultOptions, GenerateOptions, PreparePostMergeOptions>(args)
                .WithNotParsed(_ => { Environment.Exit((int)ExitCondition.InvalidArguments); })
                .WithParsedAsync(async o => { await Run(o, cancellationTokenSource); });
        }

        public static async Task Run(object commandObj, CancellationTokenSource cancellationTokenSource)
        {
            ExitCondition code = ExitCondition.Exception;

            switch (commandObj)
            {
                case PreparePostMergeOptions postMerge:
                {
                    using var provider = GetServiceProvider(false);
                    var generator = provider.GetRequiredService<Program>();
                    var log = provider.GetRequiredService<ILogger<PostMergePreparation>>();
                    try
                    {
                        var operations = new PostMergeOperations(generator, log, postMerge.RepositoryName);
                        await new PostMergePreparation(operations).RunAsync(postMerge, cancellationTokenSource.Token);
                        code = ExitCondition.Success;
                    }
                    catch (Exception ex)
                    {
                        log.LogCritical(ex, "Post-merge preparation failed. Inspect partial results and reconcile uncertain submissions before replay.");
                        code = ExitCondition.Exception;
                    }
                    break;
                }
                case GenerateOptions g:
                    var serviceProvider = GetServiceProvider(g.Debug);
                    var program = serviceProvider.GetService<Program>();
                    code = await program.RunAsync(
                        g.Organization,
                        g.Project,
                        g.Prefix,
                        g.Path,
                        g.Endpoint,
                        g.Repository,
                        g.Branch,
                        g.Agentpool,
                        g.Convention,
                        g.VariableGroups.ToArray(),
                        g.ServiceConnections,
                        g.DevOpsPath,
                        g.WhatIf,
                        g.Open,
                        g.Destroy,
                        g.NoSchedule,
                        g.SetManagedVariables,
                        g.OverwriteTriggers,
                        cancellationTokenSource.Token,
                        g.Request,
                        g.Results
                    );

                    break;
                default:
                    code = ExitCondition.InvalidArguments;
                    break;
            }

            Environment.Exit((int)code);
        }

        private static ServiceProvider GetServiceProvider(bool debug)
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

                case "tests":
                    var testLogger = serviceProvider.GetService<ILogger<IntegrationTestingPipelineConvention>>();
                    return new IntegrationTestingPipelineConvention(testLogger, context);

                default: throw new ArgumentOutOfRangeException(nameof(convention), "Could not find matching convention.");
            }
        }

        public virtual async Task<ExitCondition> RunAsync(
            string organization,
            string project,
            string prefix,
            string path,
            string endpoint,
            string repository,
            string branch,
            string agentPool,
            string convention,
            int[] variableGroups,
            IEnumerable<string> serviceConnections,
            string devOpsPath,
            bool whatIf,
            bool open,
            bool destroy,
            bool noSchedule,
            bool setManagedVariables,
            bool overwriteTriggers,
            CancellationToken cancellationToken,
            string requestPath = null,
            string resultsPath = null,
            string repositoryRoot = null)
        {
            try
            {
                if (resultsPath != null)
                {
                    File.Delete(resultsPath);
                }
                logger.LogDebug("Creating context.");

                // Fall back to a form of prefix if DevOps path is not specified
                var devOpsPathValue = string.IsNullOrEmpty(devOpsPath) ? $"\\{prefix}" : devOpsPath;

                var context = CreateContext(
                    this.logger,
                    organization,
                    project,
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
                context.Strict = requestPath != null;
                if (context.Strict && (whatIf || destroy || string.IsNullOrEmpty(resultsPath)))
                {
                    throw new ArgumentException("--request requires --results and cannot be combined with --whatif or --destroy.");
                }
                var requests = requestPath == null ? null :
                    JsonSerializer.Deserialize<DescriptorRequest[]>(File.ReadAllText(requestPath));
                if (context.Strict && (requests == null || requests.Length == 0 ||
                    requests.Any(r => r == null || string.IsNullOrEmpty(r.Path)) ||
                    requests.Select(r => r.Path).Distinct(StringComparer.Ordinal).Count() != requests.Length))
                {
                    throw new ArgumentException("The descriptor request must contain distinct nonempty targets.");
                }

                var pipelineConvention = GetPipelineConvention(convention, context);
                var components = ScanForComponents(path, pipelineConvention.SearchPattern, repositoryRoot).ToArray();
                if (requests != null)
                {
                    components = components.Where(c => requests.Any(r => r.Path == c.RelativeYamlPath.Replace('\\', '/'))).ToArray();
                    var missing = requests.Select(r => r.Path)
                        .Except(components.Select(c => c.RelativeYamlPath.Replace('\\', '/')), StringComparer.Ordinal).ToArray();
                    if (missing.Length > 0)
                    {
                        // Fail before any definition or permission change rather than preparing a subset.
                        throw new ArgumentException($"Requested descriptors were not found under '{path}': {string.Join(", ", missing)}");
                    }
                }

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

                var definitions = new List<BuildDefinition>();
                var results = new List<PipelineGenerationResult>();
                // Strict callers must not change any definition when a configured service connection is missing.
                var serviceConnectionObjects = context.Strict
                    ? await context.GetServiceConnectionsAsync(serviceConnections, cancellationToken)
                    : null;
                foreach (var component in components)
                {
                    logger.LogInformation("Processing component '{0}' in '{1}'.", component.Name, component.Path);
                    if (destroy)
                    {
                        var definition = await pipelineConvention.DeleteDefinitionAsync(component, cancellationToken);
                    }
                    else
                    {
                        pipelineConvention.PreviousPath = requests?.Single(r => r.Path == component.RelativeYamlPath.Replace('\\', '/')).PreviousPath;
                        var definition = await pipelineConvention.CreateOrUpdateDefinitionAsync(component, cancellationToken);

                        if (open)
                        {
                            OpenBrowser(definition.GetWebUrl());
                        }

                        definitions.Add(definition);
                        results.Add(new PipelineGenerationResult(component.RelativeYamlPath.Replace('\\', '/'),
                            definition.Id, convention, pipelineConvention.PipelineCategory, pipelineConvention.LastOperation));
                    }
                }

                serviceConnectionObjects ??= await context.GetServiceConnectionsAsync(serviceConnections, cancellationToken);
                var definitionIds = definitions.Select(d => d.Id).ToArray();

                foreach (var serviceConnection in serviceConnectionObjects)
                {
                    // Get set of permissions for the service connection
                    JsonNode pipelinePermissions = await context.GetPipelinePermissionsAsync(serviceConnection.Id, cancellationToken);
                    var existingCount = pipelinePermissions["pipelines"].AsArray().Count;
                    var definitionsToAdd = AddPipelinePermissions(pipelinePermissions, definitionIds, context.Strict);

                    logger.LogInformation("'{0}' pipelines already have permissions to service connection '{1}'. Need to grant permission to '{2}' more.", existingCount, serviceConnection.Id, definitionsToAdd);

                    if (definitionsToAdd > 0)
                    {
                        logger.LogInformation("Granting permissions for '{0}' definitions to service connection '{1}'.", definitionsToAdd, serviceConnection.Id);
                        // Update the permissions if we added anything
                        await context.UpdatePipelinePermissionsAsync(serviceConnection.Id, pipelinePermissions, cancellationToken);
                    }
                }

                if (context.Strict)
                {
                    foreach (var groupId in variableGroups.Distinct())
                    {
                        var permissions = await context.GetPipelinePermissionsAsync("variablegroup", groupId.ToString(), cancellationToken);
                        if (AddPipelinePermissions(permissions, definitionIds, strict: true) > 0)
                        {
                            await context.UpdatePipelinePermissionsAsync("variablegroup", groupId.ToString(), permissions, cancellationToken);
                        }
                    }
                }
                if (resultsPath != null)
                {
                    File.WriteAllText(resultsPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
                }
                return ExitCondition.Success;

            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "BOOM! Something went wrong, try running with --debug.");
                return ExitCondition.Exception;
            }
        }

        protected virtual PipelineGenerationContext CreateContext(ILogger logger, string organization, string project,
            string endpoint, string repository, string branch, string agentPool, int[] variableGroups,
            string devOpsPath, string prefix, bool whatIf, bool noSchedule, bool setManagedVariables, bool overwriteTriggers)
            => new PipelineGenerationContext(logger, organization, project, endpoint, repository, branch, agentPool,
                variableGroups, devOpsPath, prefix, whatIf, noSchedule, setManagedVariables, overwriteTriggers);

        public static int AddPipelinePermissions(JsonNode permissions, IEnumerable<int> definitionIds, bool strict)
        {
            var pipelines = permissions["pipelines"].AsArray();
            var added = 0;
            foreach (var id in definitionIds.Distinct())
            {
                var existing = pipelines.FirstOrDefault(p => p["id"].GetValue<int>() == id);
                if (existing == null)
                {
                    pipelines.Add(new JsonObject
                    {
                        ["id"] = id,
                        ["authorized"] = true,
                        ["authorizedBy"] = null,
                        ["authorizedOn"] = null
                    });
                    added++;
                }
                else if (strict && existing["authorized"]?.GetValue<bool>() != true)
                {
                    // Never override an administrator's explicit revocation; strict callers must stop before releasing.
                    throw new InvalidOperationException($"Pipeline {id} has an explicitly unauthorized resource permission; an administrator must resolve it.");
                }
            }
            return added;
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

        private IEnumerable<SdkComponent> ScanForComponents(string path, string searchPattern, string repositoryRoot)
        {
            var scanner = serviceProvider.GetService<SdkComponentScanner>();

            var scanDirectory = new DirectoryInfo(path);
            var components = scanner.Scan(scanDirectory, searchPattern, repositoryRoot);
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
