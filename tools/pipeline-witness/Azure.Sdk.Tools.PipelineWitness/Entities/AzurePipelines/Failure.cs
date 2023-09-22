using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TeamFoundation.Build.WebApi;

#nullable disable

namespace Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines
{
    public class Failure
    {
        public Failure()
        {
        }

        public Failure(TimelineRecord record, string classification)
        {
            this.Record = record;
            this.Classification = classification;
        }

        public TimelineRecord Record { get; set; }
        public string Classification { get; set; }
    }
}

