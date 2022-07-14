using Microsoft.TeamFoundation.Build.WebApi;

#nullable disable

namespace Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines
{
    public class Failure
    {
        public Failure(TimelineRecord record, string classification)
        {
            Record = record;
            Classification = classification;
        }

        public TimelineRecord Record { get; }

        public string Classification { get; }
    }
}

