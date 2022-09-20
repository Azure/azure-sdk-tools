using System;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class BuildLogBundle
    {
        public string Account { get; set; }

        public Guid ProjectId { get; set; }

        public string ProjectName { get; set; }

        public int BuildId { get; set; }

        public DateTimeOffset StartTime { get; set; }
    
        public DateTimeOffset FinishTime { get; set; }
    
        public DateTimeOffset QueueTime { get; set; }
        
        public int DefinitionId { get; set; }
        
        public string DefinitionPath { get; set; }
        
        public string DefinitionName { get; set; }

        public List<BuildLogInfo> TimelineLogs { get; } = new List<BuildLogInfo>();
    }
}

