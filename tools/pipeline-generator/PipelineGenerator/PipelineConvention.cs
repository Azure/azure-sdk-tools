using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineGenerator
{
    public abstract class PipelineConvention
    {
        public PipelineConvention(PipelineGenerationContext context)
        {
            this.Context = context;
        }

        protected PipelineGenerationContext Context { get; private set; }

        public abstract string SearchPattern { get; }
    }
}
