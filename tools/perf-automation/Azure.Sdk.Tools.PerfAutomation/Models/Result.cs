using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class Result
    {
        public string Service { get; set; }
        public string Test { get; set; }

        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public Language Language { get; set; }
        public string LanguageVersion { get; set; }
        public string Project { get; set; }
        public string LanguageTestName { get; set; }
        public string Arguments { get; set; }
        public string PrimaryPackage { get; set; }
        public IDictionary<string, string> PackageVersions { get; set; }
        public string SetupStandardOutput { get; set; }
        public string SetupStandardError { get; set; }
        public string SetupException { get; set; }

        public ICollection<IterationResult> Iterations { get; set; } = new List<IterationResult>();

        public double OperationsPerSecondMin => Iterations.Any() ? Iterations.Min(i => i.OperationsPerSecond) : -1;
        public double OperationsPerSecondMean => Iterations.Any() ? Iterations.Average(i => i.OperationsPerSecond) : -1;
        public double OperationsPerSecondMedian => Iterations.Any() ? Median(Iterations.Select(i => i.OperationsPerSecond)) : -1;
        public double OperationsPerSecondMax => Iterations.Any() ? Iterations.Max(i => i.OperationsPerSecond) : -1;

        private double Median(IEnumerable<double> values)
        {
            var count = values.Count();
            if (count % 2 == 1)
            {
                return values.OrderBy(d => d).Skip((count - 1) / 2).First();
            }
            else
            {
                return values.OrderBy(d => d).Skip((count / 2) - 1).Take(2).Average();
            }
        }
    }
}
