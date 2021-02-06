using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class Result
    {
        public string TestName { get; set; }

        public Language Language { get; set; }
        public string LanguageVersion { get; set; }
        public string Project { get; set; }
        public string LanguageTestName { get; set; }
        public string Arguments { get; set; }
        public IDictionary<string, string> PackageVersions { get; set; }
        public string SetupStandardOutput { get; set; }
        public string SetupStandardError { get; set; }

        public ICollection<IterationResult> Iterations { get; } = new List<IterationResult>();

        public double OperationsPerSecondMean => Iterations.Any() ? Iterations.Average(i => i.OperationsPerSecond) : -1;
        public double OperationsPerSecondMedian => Iterations.Any() ? Median(Iterations.Select(i => i.OperationsPerSecond)) : -1;

        private double Median(IEnumerable<double> values)
        {
            var count = values.Count();
            if (count % 2 == 1)
            {
                return values.Skip((count - 1) / 2).First();
            }
            else
            {
                return values.Skip((count / 2) - 1).Take(2).Average();
            }
        }
    }
}
