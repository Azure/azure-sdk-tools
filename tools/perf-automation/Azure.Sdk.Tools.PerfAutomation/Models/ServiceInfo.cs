using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class ServiceInfo
    {
        public string Service { get; set; }
        public IDictionary<Language, ServiceLanguageInfo> Languages { get; set; }
        public IEnumerable<TestInfo> Tests { get; set; }
    }
}
