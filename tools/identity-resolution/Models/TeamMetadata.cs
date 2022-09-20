using Azure.Sdk.Tools.NotificationConfiguration.Enums;

namespace Azure.Sdk.Tools.NotificationConfiguration.Models
{
    /// <summary>
    /// Object describing a Team in DevOps
    /// </summary>
    /// <remarks>
    /// This field is serialized into YAML and stored in the team description
    /// </remarks>
    public class TeamMetadata
    {
        /// <summary>
        /// ID of associated alert pipeline
        /// </summary>
        public int PipelineId { get; set; }

        /// <summary>
        /// Name of associated alert pipeline
        /// </summary>
        public string PipelineName { get; set; }

        /// <summary>
        /// Team's purpose
        /// </summary>
        public TeamPurpose Purpose { get; set; }
    }
}
