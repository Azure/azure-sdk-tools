using System.Collections.Generic;

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

        public int Iteration { get; set; }

        public double OperationsPerSecond { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
    }
}
