
using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class AdvancedStatistics
    {
        public int TotalOperations { get; set; }
        public double LatencyMean { get; set; } = -1;
        public double LatencyMin { get; set; } = -1;
        public double LatencyMax { get; set; } = -1;
        public Dictionary<int, double> LatencyPercentiles { get; set; }
        public double ThroughputMBpsMean { get; set; } = -1;
    }
}
