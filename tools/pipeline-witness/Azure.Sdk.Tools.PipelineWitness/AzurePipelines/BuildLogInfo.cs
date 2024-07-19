using System;

namespace Azure.Sdk.Tools.PipelineWitness.AzurePipelines
{
    public class BuildLogInfo
    {
        public int LogId { get; set; }

        public long LineCount { get; set; }

        public DateTime? LogCreatedOn { get; set; }

        public string RecordType { get; set; }

        public Guid? RecordId { get; set; }

        public Guid? ParentRecordId { get; set; }
    }
}

