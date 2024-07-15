using System;

namespace Azure.Sdk.Tools.PipelineWitness.Configuration
{
    public class PeriodicProcessSettings
    {
        /// <summary>
        /// Gets or sets whether the loop should be processed
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the amount of time between iterations of the build definition upload loop
        /// </summary>
        public TimeSpan LockLeasePeriod { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the amount of time between iterations of the loop
        /// </summary>
        public TimeSpan LoopPeriod { get; set; }

        /// <summary>
        /// Gets or sets the amount of time between to wait between successful iterations
        /// </summary>
        public TimeSpan CooldownPeriod { get; set; }

        /// <summary>
        /// Gets or sets the amount of history to process in each iteration
        /// </summary>
        public TimeSpan LookbackPeriod { get; set; }

        /// <summary>
        /// Gets or sets the name of the distributed lock
        /// </summary>
        public string LockName { get; set; }
    }
}
