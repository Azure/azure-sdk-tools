namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class ResultSummary
    {
        public Language Language { get; set; }
        public string LanguageVersion { get; set; }
        public string Service { get; set; }
        public string Test { get; set; }
        public string Arguments { get; set; }

        public string LastVersion { get; set; }
        public double Last { get; set; }
        public double Source { get; set; }
    }
}
