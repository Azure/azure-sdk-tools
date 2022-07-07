namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class CsvResult
    {
        public string Service { get; set; }
        public string Test { get; set; }
        public Language Language { get; set; }
        public long Size { get; set; }
        public int Count { get; set; }
        public int Parallel { get; set; }
        public string PackageVersions { get; set; }
        public double OperationsPerSecondMax { get; set; }
    }
}
