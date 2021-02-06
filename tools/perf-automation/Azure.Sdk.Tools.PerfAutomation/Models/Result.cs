using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class Result
    {
        public string TestName { get; set; }

        public Language Language { get; set; }
        public string Project { get; set; }
        public string LanguageTestName { get; set; }
        public string Arguments { get; set; }
        public IDictionary<string, string> PackageVersions { get; set; }
        public string SetupStandardOutput { get; set; }
        public string SetupStandardError { get; set; }

        public ICollection<IterationResult> Iterations { get; } = new List<IterationResult>();

        public double OperationsPerSecondAverage => Iterations.Any() ? Iterations.Average(i => i.OperationsPerSecond) : -1;
    }
}
