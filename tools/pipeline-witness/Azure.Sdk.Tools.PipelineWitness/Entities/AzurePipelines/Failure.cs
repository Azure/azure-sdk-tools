using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

namespace Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines
{
    public class Failure
    {
        public Failure()
        {
        }

        public Failure(string scope, string classification)
        {
            this.Scope = scope;
            this.Classification = classification;
        }

        public string Scope { get; set; }
        public string Classification { get; set; }
    }
}
