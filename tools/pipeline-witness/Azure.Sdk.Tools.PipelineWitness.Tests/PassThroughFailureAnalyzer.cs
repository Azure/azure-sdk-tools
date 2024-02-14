using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.Tests
{
    internal class PassThroughFailureAnalyzer : IFailureAnalyzer
    {
        public Task<IEnumerable<Failure>> AnalyzeFailureAsync(Build build, Timeline timeline)
        {
            return Task.FromResult<IEnumerable<Failure>>(new List<Failure>());
        }
    }
}
