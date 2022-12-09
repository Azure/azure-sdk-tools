using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class LanguageServiceInput
    {
        public string Service { get; set; }
        public string Project { get; set; }
        public IEnumerable<IDictionary<string, string>> PackageVersions { get; set; }
        public IEnumerable<LanguageServiceTestInfo> Tests { get; set; }
    }
}
