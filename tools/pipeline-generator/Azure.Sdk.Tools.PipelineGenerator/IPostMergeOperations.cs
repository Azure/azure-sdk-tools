using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using PipelineGenerator.CommandParserOptions;

namespace PipelineGenerator
{
    public interface IPostMergeOperations
    {
        Task<IReadOnlyList<PipelineGenerationResult>> PrepareAsync(PreparePostMergeOptions options,
            DescriptorRequest[] descriptors, string branch, CancellationToken cancellationToken);
        Task<Build> FindBuildAsync(int definitionId, string repository, string sha, string branch, CancellationToken cancellationToken);
        Task<Build> GetBuildAsync(int runId, CancellationToken cancellationToken);
        Task<Build> QueueBuildAsync(int definitionId, string repository, string sha, string branch, CancellationToken cancellationToken);
    }
}
