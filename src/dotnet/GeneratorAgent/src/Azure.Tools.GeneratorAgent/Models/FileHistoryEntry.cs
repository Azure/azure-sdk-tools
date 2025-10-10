namespace Azure.Tools.GeneratorAgent.Models
{
    /// <summary>
    /// Represents a historical version of a TypeSpec file for rollback purposes
    /// </summary>
    public class FileHistoryEntry
    {
        /// <summary>
        /// Version number when this content was active
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// The file content at this version
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// SHA256 hash of the content for integrity verification
        /// </summary>
        public string? Sha256 { get; set; }
    }
}