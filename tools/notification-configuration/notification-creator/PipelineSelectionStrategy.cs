namespace Azure.Sdk.Tools.NotificationConfiguration
{
    /// <summary>
    /// Describes strategy to use when selecting pipelines
    /// </summary>
    public enum PipelineSelectionStrategy
    {
        /// <summary>
        /// Includes only pipelines which have a schedule
        /// </summary>
        Scheduled = 0,

        /// <summary>
        /// Includes all pipelines
        /// </summary>
        All = 1,
    }
}
