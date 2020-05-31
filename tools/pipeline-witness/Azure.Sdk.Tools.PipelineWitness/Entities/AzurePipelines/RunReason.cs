using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines
{
    public enum RunReason
    {
        Manual,
        Scheduled,
        ContinuousIntegration,
        PullRequest,
        Other
    }
}
