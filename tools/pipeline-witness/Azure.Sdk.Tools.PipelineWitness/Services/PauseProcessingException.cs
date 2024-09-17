using System;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    internal class PauseProcessingException : Exception
    {
        public TimeSpan PauseDuration { get; set; }

        public PauseProcessingException(TimeSpan pauseDuration) : base()
        {
            PauseDuration = pauseDuration;
        }
    }
}
