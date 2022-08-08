using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class Input
    {
        public IDictionary<Language, LanguageInfo> Languages { get; set; }
        public IEnumerable<ServiceInfo> Services { get; set; }
    }
}
