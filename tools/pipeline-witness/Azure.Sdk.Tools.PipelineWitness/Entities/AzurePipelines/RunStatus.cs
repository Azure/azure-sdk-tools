using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines
{
    public enum RunStatus
    {
        None,
        InProgress,
        Completed,
        Cancelling,
        Postponed,
        NotStarted
    }
}
