using System;

namespace Azure.Sdk.Tools.PipelineWitness.Entities
{
    public class BuildLogInfo
    {
        public int LogId { get; set; }

        public DateTimeOffset LogCreatedOn { get; set; }

        public Guid? RecordId { get; set; }
    }
}

