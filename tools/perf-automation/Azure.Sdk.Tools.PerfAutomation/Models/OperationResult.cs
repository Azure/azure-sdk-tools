
namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class OperationResult
    {
        /// <summary>
        /// The time taken to complete the operation in seconds.
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// The number of bytes processed by the operation.
        /// </summary>
        public long Size { get; set; }
    }
}
