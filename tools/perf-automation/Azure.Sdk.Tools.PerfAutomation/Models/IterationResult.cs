using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class IterationResult
    {
        public IDictionary<string, string> PackageVersions { get; set; }
        public double OperationsPerSecond { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public string Exception { get; set; }
    }
}
