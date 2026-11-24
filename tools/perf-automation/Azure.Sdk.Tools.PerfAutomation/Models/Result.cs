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

        private IEnumerable<double> SuccessfulOperationsPerSecond =>
            Iterations.Select(i => i.OperationsPerSecond).Where(value => value != -1);

        public double OperationsPerSecondMin => SuccessfulOperationsPerSecond.DefaultIfEmpty(-1).Min();
        public double OperationsPerSecondMean => SuccessfulOperationsPerSecond.DefaultIfEmpty(-1).Average();
        public double OperationsPerSecondMedian => Median(SuccessfulOperationsPerSecond.DefaultIfEmpty(-1));
        public double OperationsPerSecondMax => SuccessfulOperationsPerSecond.DefaultIfEmpty(-1).Max();

        public double CpuMean => Iterations.Any() ? Math.Round(Iterations.Average(i => i.AverageCpu), 2) : -1;
        public long MemoryMean => Iterations.Any() ? (long)Iterations.Average(i => i.AverageMemory) : -1;

        public AdvancedStatistics AdvancedStatistics { get; set; } = null;

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
