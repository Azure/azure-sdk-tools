using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PipelineGenerator.Conventions
{
    public abstract class PipelineConvention
    {
        public PipelineConvention(ILogger logger, PipelineGenerationContext context)
        {
            Logger = logger;
            Context = context;
        }

        // Organization id for dev.azure.com/azure-sdk
        // From https://ado-org-api.prod.space.microsoft.com/orgs/azure-sdk
        // See https://dev.azure.com/mseng/1ES/_wiki/wikis/SaCE%20Team%20Wiki/13937/How-to-obtain-Azure-DevOps-organization-ID-or-name
        private const string PipelineClassificationOrganizationId = "0fb41ef4-5012-48a9-bf39-4ee3de03ee35";
        private const string PipelineClassificationBulkTagUpdateUrl = "https://artifact-tags-api.prod.space.microsoft.com/tags/bulk";
        // Service Tree ID for Azure SDK in the product catalog
        private const string ProductCatalogServiceId = "4405e061-966a-4249-afdd-f7435f54a510";
        private const string ProductCatalogAssociationUrl = $"https://product-catalog-api.pmeprod.space.microsoft.com/products/{ProductCatalogServiceId}/artifacts";

        private const string ReportBuildStatusKey = "reportBuildStatus";

        private Dictionary<string, BuildDefinitionReference> pipelineReferences;
        private readonly HashSet<string> definitions = new();
        private readonly HashSet<string> pipelineClassifications = new();

        protected ILogger Logger { get; }
        protected PipelineGenerationContext Context { get; }
        public abstract string SearchPattern { get; }
        public abstract string PipelineNameSuffix { get; }
        public abstract string PipelineCategory { get; }
        // 1es required classification. Uses external API to set classification
        public enum PipelineClassifications { Production, NonProduction, Empty }
        public abstract PipelineClassifications Classification { get; }

        public class TagRequest
        {
            [JsonPropertyName("artifactId")]
            public string ArtifactId { get; set; }
            [JsonPropertyName("artifactType")]
            public string ArtifactType { get; set; }
            [JsonPropertyName("tags")]
            public List<string> Tags { get; set; }
            [JsonPropertyName("autoClassificationSource")]
            public string AutoClassificationSource { get; set; }
        }

        public class SaveTagRequests
        {
            [JsonPropertyName("saveTagRequests")]
            public List<TagRequest> Requests { get; set; }
        }

        public string GetDefinitionName(SdkComponent component)
        {
            var baseName = component.Variant == null
                            ? $"{Context.Prefix} - {component.Name}"
                            : $"{Context.Prefix} - {component.Name} - {component.Variant}";
            return baseName + PipelineNameSuffix;
        }

        private string GetChangesKey(BuildDefinition definition)
        {
            return $"{definition.Name}/{definition.Id}";
        }

        public bool HasDefinitionChanges(BuildDefinition definition)
        {
            return this.definitions.Contains(GetChangesKey(definition));
        }

        public void RequireDefinitionChanges(BuildDefinition definition)
        {
            this.definitions.Add(GetChangesKey(definition));
        }

        public bool HasClassificationChanges(BuildDefinition definition)
        {
            return this.pipelineClassifications.Contains(GetChangesKey(definition));
        }

        public void RequireClassificationChanges(BuildDefinition definition)
        {
            this.pipelineClassifications.Add(GetChangesKey(definition));
        }


        public async Task<BuildDefinition> DeleteDefinitionAsync(SdkComponent component, CancellationToken cancellationToken)
        {
            var definitionName = GetDefinitionName(component);

            Logger.LogDebug("Checking to see if definition '{0}' exists prior to deleting.", definitionName);
            var definition = await GetExistingDefinitionAsync(definitionName, cancellationToken);

            if (definition != null)
            {
                Logger.LogDebug("Found definition called '{0}' at '{1}'.", definitionName, definition.GetWebUrl());

                if (!Context.WhatIf)
                {
                    Logger.LogWarning("Deleting definition '{0}'.", definitionName);
                    var projectReference = await Context.GetProjectReferenceAsync(cancellationToken);
                    var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
                    await buildClient.DeleteDefinitionAsync(
                        project: projectReference.Id,
                        definitionId: definition.Id,
                        cancellationToken: cancellationToken
                        );
                }
                else
                {
                    Logger.LogWarning("Skipping deleting definition '{0}' (--whatif).", definitionName);
                }

                return definition;
            }
            else
            {
                Logger.LogDebug("No definition called '{0}' existed.", definitionName);
                return null;
            }
        }

        public async Task<BuildDefinition> CreateOrUpdateDefinitionAsync(SdkComponent component, CancellationToken cancellationToken)
        {
            var definitionName = GetDefinitionName(component);

            Logger.LogDebug("Checking to see if definition '{0}' exists prior to create/update.", definitionName);
            var definition = await GetExistingDefinitionAsync(definitionName, cancellationToken);

            if (definition == null)
            {
                Logger.LogDebug("Definition '{0}' was not found.", definitionName);
                definition = await CreateDefinitionAsync(definitionName, component, cancellationToken);
                if (Context.SetPipelineClassification)
                {
                    RequireClassificationChanges(definition);
                }
            }
            if (Context.ForcePipelineClassification)
            {
                RequireClassificationChanges(definition);
            }

            Logger.LogDebug("Applying convention to '{0}' definition.", definitionName);
            await ApplyConventionAsync(definition, component);

            if (HasDefinitionChanges(definition) || Context.OverwriteTriggers)
            {
                if (!Context.WhatIf)
                {
                    Logger.LogInformation("Convention had changes, updating '{0}' definition.", definitionName);
                    var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
                    definition.Comment = "Updated by pipeline generation tool";
                    definition = await buildClient.UpdateDefinitionAsync(
                        definition: definition,
                        cancellationToken: cancellationToken
                        );
                }
                else
                {
                    Logger.LogWarning("Skipping update to definition '{0}' (--whatif).", definitionName);
                }
            }
            else
            {
                Logger.LogDebug("No changes for definition '{0}'.", definitionName);
            }

            return definition;
        }

        public async Task UpdatePipelineClassifications(List<BuildDefinition> definitions)
        {
            if (!Context.ForcePipelineClassification && !Context.SetPipelineClassification)
            {
                Logger.LogInformation("Skipping 1es pipeline classification as --set-pipeline-classification or --force-pipeline-classification was not set.");
                return;
            }

            if (string.IsNullOrEmpty(Context.ProductCatalogTokenEnvVar))
            {
                throw new Exception("No product catalog token environment variable specified, cannot perform 1es pipeline classification.");
            }
            else if (definitions.Count == 0)
            {
                return;
            }

            var allTagRequests = new List<TagRequest>();

            foreach (var definition in definitions)
            {
                if (!HasClassificationChanges(definition) && !Context.ForcePipelineClassification)
                {
                    continue;
                }

                var projectId = definition.Project.Id.ToString();
                var artifactId = $"vsts://{PipelineClassificationOrganizationId}/{projectId}/{definition.Id}";
                var tagRequest = new TagRequest
                {
                    ArtifactId = artifactId,
                    ArtifactType = "Microsoft.AzureDevOps/BuildDefinition",
                    // Accepted classifications are 'Production' and 'NonProduction'
                    Tags = new List<string>(),
                    AutoClassificationSource = "azure-sdk-pipeline-generator"
                };
                if (this.Classification.ToString() != PipelineClassifications.Empty.ToString())
                {
                    tagRequest.Tags.Add(this.Classification.ToString());
                }
                allTagRequests.Add(tagRequest);
            }

            if (allTagRequests.Count == 0)
            {
                return;
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            var token = Environment.GetEnvironmentVariable(Context.ProductCatalogTokenEnvVar);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            Logger.LogInformation($"Updating {allTagRequests.Count} pipeline classifications at {PipelineClassificationBulkTagUpdateUrl}");

            // The product classification bulk API takes a very long time, so use small update batches
            var batchSize = 25;
            var batch = allTagRequests.Take(batchSize);
            while (batch.Any())
            {
                // See https://eng.ms/docs/cloud-ai-platform/devdiv/one-engineering-system-1es/1es-docs/product-catalog/how-to/use-product-catalog-api
                // Schema - https://artifact-tags-api.prod.space.microsoft.com/swagger/index.html?url=/swagger/v1/swagger.json#/Tags/put_tags
                var bulkRequest = new SaveTagRequests { Requests = batch.ToList() };
                var json = JsonSerializer.Serialize(bulkRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");
                if (Context.WhatIf)
                {
                    Logger.LogInformation("[WHATIF] Batching {Count} pipeline classifications", batch.Count());
                    foreach (var entry in batch)
                    {
                        var pretty = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
                        Logger.LogDebug("{pretty}", pretty);
                    }
                } else
                {
                    Logger.LogInformation("Batching {Count} pipeline classifications", batch.Count());
                    var sw = new Stopwatch();
                    sw.Start();
                    var response = await client.PutAsync(PipelineClassificationBulkTagUpdateUrl, content);
                    response.EnsureSuccessStatusCode();
                    sw.Stop();
                    Logger.LogInformation($"Batched {batch.Count()} pipeline classifications in {sw.ElapsedMilliseconds / 1000}s");
                    if (response.Content.Headers.ContentType.MediaType != "application/json")
                    {
                        throw new Exception("Did not receive json response from 1es pipeline classification API. " +
                                            "This is likely due to an invalid bearer token returning a login page for a browser.");
                    }
                }
                Logger.LogInformation("Updating product catalog/service tree associations for {Count} pipelines", batch.Count());
                foreach (var pipeline in batch)
                {
                    var productJson = JsonSerializer.Serialize(new {
                        artifactId = pipeline.ArtifactId,
                        artifactType = pipeline.ArtifactType
                    });
                    var productContent = new StringContent(productJson, Encoding.UTF8, "application/json");
                    if (Context.WhatIf)
                    {
                        Logger.LogInformation("[WHATIF] Updating product catalog association for service {ProductCatalogServiceId}:", ProductCatalogServiceId);
                        Logger.LogInformation("[WHATIF] {productJson}", productJson);
                    }
                    else
                    {
                        var updateUrl = ProductCatalogAssociationUrl + $"?artifactType={pipeline.ArtifactType}&artifactId={pipeline.ArtifactId}";
                        var productSw = new Stopwatch();
                        productSw.Start();
                        var productResponse = await client.PutAsync(updateUrl, productContent);
                        productResponse.EnsureSuccessStatusCode();
                        productSw.Stop();
                        Logger.LogInformation($"Updated product catalog association in {productSw.ElapsedMilliseconds / 1000}s");
                    }
                }

                if (batch.Count() < batchSize)
                {
                    break;
                }
                allTagRequests = allTagRequests.GetRange(batchSize, allTagRequests.Count - batchSize);
                batch = allTagRequests.Take(batchSize);
            }

            return;
        }

        private async Task<BuildDefinition> GetExistingDefinitionAsync(string definitionName, CancellationToken cancellationToken)
        {
            Logger.LogDebug("Attempting to get existing definition '{0}'.", definitionName);
            var projectReference = await Context.GetProjectReferenceAsync(cancellationToken);
            var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);

            if (pipelineReferences == default)
            {
                var definitionReferences = await buildClient.GetDefinitionsAsync(
                    project: projectReference.Id,
                    path: Context.DevOpsPath
                    );

                pipelineReferences = new Dictionary<string, BuildDefinitionReference>();
                foreach (var definition in definitionReferences)
                {
                    if (pipelineReferences.ContainsKey(definition.Name))
                    {
                        Logger.LogDebug($"Found more then one definition with name {definition.Name}, picking the first one {pipelineReferences[definition.Name].Id} and not {definition.Id}");
                    }
                    else
                    {
                        pipelineReferences.Add(definition.Name, definition);
                    }
                }
                Logger.LogDebug($"Cached {definitionReferences.Count} pipelines.");
            }

            BuildDefinitionReference definitionReference = null;
            pipelineReferences.TryGetValue(definitionName, out definitionReference);

            if (definitionReference != null)
            {
                Logger.LogDebug("Existing definition '{0}' found at '{1}'.", definitionName, definitionReference.GetWebUrl());
                return await buildClient.GetDefinitionAsync(
                        project: projectReference.Id,
                        definitionId: definitionReference.Id,
                        cancellationToken: cancellationToken
                        );
            }
            else
            {
                Logger.LogDebug("No definition named '{0}' was found.", definitionName);
                return null;
            }
        }

        private async Task<BuildDefinition> CreateDefinitionAsync(string definitionName, SdkComponent component, CancellationToken cancellationToken)
        {
            var serviceEndpoint = await Context.GetServiceEndpointAsync(cancellationToken);

            var repository = Context.Repository;

            var buildRepository = new BuildRepository
            {
                DefaultBranch = Context.Branch,
                Id = repository,
                Name = repository,
                Type = "GitHub",
                Url = new Uri($"https://github.com/{repository}.git"),
                Properties = { ["connectedServiceId"] = serviceEndpoint.Id.ToString() }
            };

            var projectReference = await Context.GetProjectReferenceAsync(cancellationToken);
            var agentPoolQueue = await Context.GetAgentPoolQueue(cancellationToken);
            var normalizedRelativeYamlPath = component.RelativeYamlPath.Replace("\\", "/");

            var definition = new BuildDefinition()
            {
                Name = definitionName,
                Project = projectReference,
                Path = Context.DevOpsPath,
                Repository = buildRepository,
                Process = new YamlProcess()
                {
                    YamlFilename = normalizedRelativeYamlPath
                },
                Queue = agentPoolQueue
            };

            if (!Context.WhatIf)
            {
                Logger.LogDebug("Creating definition named '{0}'.", definitionName);

                var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
                definition = await buildClient.CreateDefinitionAsync(
                    definition: definition,
                    cancellationToken: cancellationToken
                    );

                Logger.LogInformation("Created definition '{0}' at: {1}", definitionName, definition.GetWebUrl());
            }
            else
            {
                Logger.LogWarning("Skipping creating definition '{0}' (--whatif).", definitionName);
            }

            return definition;
        }

        protected void EnsureManagedVariables(BuildDefinition definition, SdkComponent component)
        {
            if (!Context.SetManagedVariables)
            {
                return;
            }

            var managedVariables = new Dictionary<string, string>
            {
                { "meta.platform", Context.Prefix },
                { "meta.component", component.Name },
                { "meta.variant", component.Variant },
                { "meta.category", this.PipelineCategory },
                { "meta.autoGenerated", "true" },
            };

            foreach (var (key, value) in managedVariables)
            {
                if (string.IsNullOrEmpty(value))
                {
                    if (definition.Variables.ContainsKey(key))
                    {
                        Logger.LogInformation("Removing managed variable {Name}", key);
                        definition.Variables.Remove(key);
                        RequireDefinitionChanges(definition);
                    }

                    // else: Nothing to do if an empty variable doesn't already exist.
                    continue;
                }

                if (definition.Variables.TryGetValue(key, out var existingVariable))
                {
                    if (existingVariable.Value == value && !existingVariable.AllowOverride && !existingVariable.IsSecret)
                    {
                        // nothing to do if an existing variable matches the new value and options
                        continue;
                    }

                    Logger.LogInformation("Overwriting managed variable {Name} from '{OriginalValue}' to '{NewValue}', not secret, not overridable", key, existingVariable.Value, value);
                }

                definition.Variables[key] = new BuildDefinitionVariable { Value = value, IsSecret = false, AllowOverride = false };
                RequireDefinitionChanges(definition);
            }
        }

        protected void EnsureVariableGroups(BuildDefinition definition)
        {
            var definitionVariableGroupSet = definition.VariableGroups
                .Select(group => group.Id)
                .ToHashSet();

            var parameterGroupSet = Context.VariableGroups.ToHashSet();

            var idsToAdd = parameterGroupSet.Except(definitionVariableGroupSet);
            if (idsToAdd.Any())
            {
                RequireDefinitionChanges(definition);
            }
            var groupsToAdd = idsToAdd.Select(id => new VariableGroup { Id = id });

            definition.VariableGroups.AddRange(groupsToAdd);
        }

        private void EnsureReportBuildStatus(BuildDefinition definition)
        {
            if (definition.Repository.Properties.TryGetValue(ReportBuildStatusKey, out var reportBuildStatusString))
            {
                if (!bool.TryParse(reportBuildStatusString, out var reportBuildStatusValue) || !reportBuildStatusValue)
                {
                    definition.Repository.Properties[ReportBuildStatusKey] = "true";
                    RequireDefinitionChanges(definition);
                }
            }
            else
            {
                definition.Repository.Properties.Add(ReportBuildStatusKey, "true");
                RequireDefinitionChanges(definition);
            }
        }

        protected const int FirstSchedulingHour = 0;
        protected const int LastSchedulingHour = 24;
        protected const int TotalHours = LastSchedulingHour - FirstSchedulingHour;
        protected const int TotalMinutes = TotalHours * 60;
        protected const int BucketSizeInMinutes = 15;
        protected const int TotalBuckets = TotalMinutes / BucketSizeInMinutes;
        protected const int BucketsPerHour = 60 / BucketSizeInMinutes;

        protected virtual Schedule CreateScheduleFromDefinition(BuildDefinition definition)
        {
            var bucket = definition.Id % TotalBuckets;
            var startHours = bucket / BucketsPerHour;
            var startMinutes = bucket % BucketsPerHour;

            var schedule = new Schedule
            {
                DaysToBuild = (ScheduleDays)31, // Schedule M-F
                ScheduleOnlyWithChanges = true,
                StartHours = FirstSchedulingHour + startHours,
                StartMinutes = startMinutes * BucketSizeInMinutes,
                TimeZoneId = "Pacific Standard Time",
            };
            schedule.BranchFilters.Add($"+{Context.Branch}");

            return schedule;
        }

        protected virtual Task ApplyConventionAsync(BuildDefinition definition, SdkComponent component)
        {
            EnsureVariableGroups(definition);
            EnsureManagedVariables(definition, component);
            EnsureReportBuildStatus(definition);
            EnsureDefinitionProperties(definition);
            return Task.CompletedTask;
        }

        protected void EnsureDefinitionProperties(BuildDefinition definition)
        {
            if (definition.Path != Context.DevOpsPath)
            {
                definition.Path = Context.DevOpsPath;
                RequireDefinitionChanges(definition);
            }

            if (definition.Repository.Properties.TryGetValue(ReportBuildStatusKey, out var reportBuildStatusString))
            {
                if (!bool.TryParse(reportBuildStatusString, out var reportBuildStatusValue) || !reportBuildStatusValue)
                {
                    definition.Repository.Properties[ReportBuildStatusKey] = "true";
                    RequireDefinitionChanges(definition);
                }
            }
            else
            {
                definition.Repository.Properties.Add(ReportBuildStatusKey, "true");
                RequireDefinitionChanges(definition);
            }
        }

        protected void EnsureDefaultPullRequestTrigger(BuildDefinition definition, bool overrideYaml = true, bool securePipeline = true)
        {
            var prTriggers = definition.Triggers.OfType<PullRequestTrigger>();
            if (prTriggers == default || !prTriggers.Any())
            {
                var newTrigger = new PullRequestTrigger();

                if (overrideYaml)
                {
                    newTrigger.SettingsSourceType = 1; // Override what is in the yaml file and use what is in the pipeline definition
                    newTrigger.BranchFilters.Add("+*");
                }
                else
                {
                    newTrigger.SettingsSourceType = 2; // Pull settings from yaml
                }

                newTrigger.Forks = new Forks
                {
                    AllowSecrets = securePipeline,
                    Enabled = true
                };

                newTrigger.RequireCommentsForNonTeamMembersOnly = false;
                newTrigger.IsCommentRequiredForPullRequest = securePipeline;

                definition.Triggers.Add(newTrigger);
                RequireDefinitionChanges(definition);
            }
            else
            {
                foreach (var trigger in prTriggers)
                {
                    if (overrideYaml)
                    {
                        // Override what is in the yaml file and use what is in the pipeline definition
                        if (trigger.SettingsSourceType != 1)
                        {
                            trigger.SettingsSourceType = 1;
                            RequireDefinitionChanges(definition);
                        }

                        // If any branch filters exist then overwrite them to the most generous filter.
                        // The filter should support all branches because PR triggers with a yaml override
                        // like this are expected to be manually invoked by `/azp run` comments, and these PRs
                        // may be targeting development branches.
                        if (!trigger.BranchFilters.SequenceEqual(new List<string> { "+*" }))
                        {
                            var filters = trigger.BranchFilters.Select(f => $"'{f}'");
                            Logger.LogInformation($"Overwriting branch filters ({String.Join(", ", filters)}) for PR trigger with '+*'");
                            trigger.BranchFilters.Clear();
                            trigger.BranchFilters.Add("+*");
                            RequireDefinitionChanges(definition);
                        }
                    }
                    else if (trigger.SettingsSourceType != 2)
                    {
                        // Pull settings from yaml
                        trigger.SettingsSourceType = 2;
                        RequireDefinitionChanges(definition);
                    }
                    if (trigger.RequireCommentsForNonTeamMembersOnly != false ||
                       trigger.Forks.AllowSecrets != securePipeline ||
                       trigger.Forks.Enabled != true ||
                       trigger.IsCommentRequiredForPullRequest != securePipeline
                       )
                    {
                        trigger.Forks.AllowSecrets = securePipeline;
                        trigger.Forks.Enabled = true;
                        trigger.RequireCommentsForNonTeamMembersOnly = false;
                        trigger.IsCommentRequiredForPullRequest = securePipeline;

                        RequireDefinitionChanges(definition);
                    }
                }
            }
        }

        protected void EnsureDefaultScheduledTrigger(BuildDefinition definition)
        {
            if (Context.NoSchedule)
            {
                return;
            }

            var scheduleTriggers = definition.Triggers.OfType<ScheduleTrigger>();

            // Only add the schedule trigger if one doesn't exist.
            if (scheduleTriggers == default || !scheduleTriggers.Any() || Context.OverwriteTriggers)
            {
                var computedSchedule = CreateScheduleFromDefinition(definition);

                definition.Triggers.RemoveAll(e => e is ScheduleTrigger);
                definition.Triggers.Add(new ScheduleTrigger
                {
                    Schedules = new List<Schedule> { computedSchedule }
                });

                RequireDefinitionChanges(definition);
            }
        }

        protected void EnsureDefaultCITrigger(BuildDefinition definition)
        {
            var ciTrigger = definition.Triggers.OfType<ContinuousIntegrationTrigger>().SingleOrDefault();
            if (ciTrigger == null || Context.OverwriteTriggers)
            {
                definition.Triggers.RemoveAll(e => e is ContinuousIntegrationTrigger);
                definition.Triggers.Add(new ContinuousIntegrationTrigger()
                {
                    SettingsSourceType = 2 // Get CI trigger data from yaml file
                });
                RequireDefinitionChanges(definition);
            }
            else
            {
                if (ciTrigger.SettingsSourceType != 2)
                {
                    ciTrigger.SettingsSourceType = 2;
                    RequireDefinitionChanges(definition);
                }
            }
        }
    }
}

