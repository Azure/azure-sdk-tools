using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class ResultSummary
    {
        public Language Language { get; set; }
        public string LanguageVersion { get; set; }
        public string Service { get; set; }

        public string Test { get; set; }
        public string Arguments { get; set; }

        public string PrimaryPackage { get; set; }
        public IEnumerable<IDictionary<string, string>> RequestedPackageVersions { get; set; }
        public IEnumerable<IDictionary<string, string>> RuntimePackageVersions { get; set; }

        public IEnumerable<(string version, double operationsPerSecond)> OperationsPerSecondMax { get; set; }
        public IEnumerable<(string version, double operationsPerSecond)> OperationsPerSecondMean { get; set; }

        public IEnumerable<double> OperationsPerSecondMaxDifferences =>
            OperationsPerSecondMax.Zip(OperationsPerSecondMax.Skip(1), (first, second) => ((second.operationsPerSecond / first.operationsPerSecond) - 1));

        public IEnumerable<double> OperationsPerSecondMeanDifferences =>
            OperationsPerSecondMean.Zip(OperationsPerSecondMean.Skip(1), (first, second) => ((second.operationsPerSecond / first.operationsPerSecond) - 1));
    }
}
