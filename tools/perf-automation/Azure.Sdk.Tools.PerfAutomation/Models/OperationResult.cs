using System;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class OperationResult
    {
        public TimeSpan Time { get; set; }
        public long Size { get; set; }
    }
}
