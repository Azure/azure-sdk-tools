using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class LanguageInfo
    {
        public IEnumerable<string> DefaultVersions { get; set; }
        public IEnumerable<string> OptionalVersions { get; set; }
    }
}
