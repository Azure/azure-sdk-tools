using PipelineBulkEditor.Common.Enums;

namespace PipelineBulkEditor.Common.Models
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
        /// Team's purpose
        /// </summary>
        public TeamPurpose Purpose { get; set; }
    }
}
