#nullable disable

namespace Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines
{
    using Microsoft.TeamFoundation.Build.WebApi;
    
    public class Failure
    {
        public Failure(TimelineRecord record, string classification)
        {
            this.Record = record;
            this.Classification = classification;
        }

        public TimelineRecord Record { get; }
        
        public string Classification { get; }
    }
}

