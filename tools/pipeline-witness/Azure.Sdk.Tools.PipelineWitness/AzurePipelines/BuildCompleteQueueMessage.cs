using System;

namespace Azure.Sdk.Tools.PipelineWitness.AzurePipelines
{
    public class BuildCompleteQueueMessage
    {
        public string Account { get; set; }

        public Guid ProjectId { get; set; }

        public int BuildId { get; set; }
    }
}
