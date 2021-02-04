using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class ServiceInfo
    {
        public string Service { get; set; }
        public IDictionary<Language, LanguageInfo> Languages { get; set; }
        public IList<TestInfo> Tests { get; set; }
    }

}
