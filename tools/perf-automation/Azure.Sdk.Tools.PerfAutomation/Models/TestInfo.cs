using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class TestInfo
    {
        public string Test { get; set; }
        public IList<string> Arguments { get; set; }
        public IDictionary<Language, string> TestNames { get; set; }
    }
}
