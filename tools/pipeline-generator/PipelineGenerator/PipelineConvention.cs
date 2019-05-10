using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PipelineGenerator
{
    public abstract class PipelineConvention
    {
        public PipelineConvention(PipelineGenerationContext context)
        {
            this.Context = context;
        }

        protected PipelineGenerationContext Context { get; private set; }

        public abstract string SearchPattern { get; }

        protected abstract string GetDefinitionName(SdkComponent component);

        public async Task<BuildDefinition> DeleteDefinitionAsync(SdkComponent component, CancellationToken cancellationToken)
        {
            var definitionName = GetDefinitionName(component);
            var definition = await GetExistingDefinitionAsync(definitionName, cancellationToken);

            if (definition != null)
            {
                if (!Context.WhatIf)
                {
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
                    // TODO: Logging for what if.
                }

                return definition;
            }
            else
            {
                return null;
            }
        }

        public async Task<BuildDefinition> CreateOrUpdateDefinitionAsync(SdkComponent component, CancellationToken cancellationToken)
        {
            var definitionName = GetDefinitionName(component);
            var definition = await GetExistingDefinitionAsync(definitionName, cancellationToken);

            if (definition == null)
            {
                definition = await CreateDefinitionAsync(definitionName, component, cancellationToken);
            }

            var hasChanges = await ApplyConventionAsync(definition, component);

            if (hasChanges)
            {
                var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
                definition = await buildClient.UpdateDefinitionAsync(
                    definition: definition,
                    cancellationToken: cancellationToken
                    );
            }

            return definition;
        }

        private async Task<BuildDefinition> GetExistingDefinitionAsync(string definitionName, CancellationToken cancellationToken)
        {
            var projectReference = await Context.GetProjectReferenceAsync(cancellationToken);
            var sourceRepository = await Context.GetSourceRepositoryAsync(cancellationToken);
            var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
            var definitionReferences = await buildClient.GetDefinitionsAsync(
                project: projectReference.Id,
                name: definitionName,
                repositoryId: sourceRepository.Id,
                repositoryType: "github"
                );
            var definitionReference = definitionReferences.SingleOrDefault();

            if (definitionReference != null)
            {
                return await buildClient.GetDefinitionAsync(
                        project: projectReference.Id,
                        definitionId: definitionReference.Id,
                        cancellationToken: cancellationToken
                        );
            }
            else
            {
                return null;
            }
        }

        private async Task<BuildDefinition> CreateDefinitionAsync(string definitionName, SdkComponent component, CancellationToken cancellationToken)
        {
            var sourceRepository = await Context.GetSourceRepositoryAsync(cancellationToken);

            var buildRepository = new BuildRepository()
            {
                DefaultBranch = "refs/heads/master",
                Id = sourceRepository.Id,
                Name = sourceRepository.FullName,
                Type = "GitHub",
                Url = new Uri(sourceRepository.Properties["cloneUrl"]),
            };

            buildRepository.Properties.AddRangeIfRangeNotNull(sourceRepository.Properties);

            var projectReference = await Context.GetProjectReferenceAsync(cancellationToken);
            var agentPoolQueue = await Context.GetAgentPoolQueue(cancellationToken);

            var definition = new BuildDefinition()
            {
                Name = definitionName,
                Project = projectReference,
                Repository = buildRepository,
                Process = new YamlProcess()
                {
                    YamlFilename = component.RelativeYamlPath
                },
                Queue = agentPoolQueue
            };

            if (!Context.WhatIf)
            {
                var buildClient = await Context.GetBuildHttpClientAsync(cancellationToken);
                definition = await buildClient.CreateDefinitionAsync(
                    definition: definition,
                    cancellationToken: cancellationToken
                    );

            }

            return definition;
        }

        protected abstract Task<bool> ApplyConventionAsync(BuildDefinition definition, SdkComponent component);
    }
}
