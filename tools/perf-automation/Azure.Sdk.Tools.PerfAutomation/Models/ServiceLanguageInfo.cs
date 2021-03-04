using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class ServiceLanguageInfo
    {
        public string Project { get; set; }
        public IEnumerable<IDictionary<string, string>> PackageVersions { get; set; }
        public IDictionary<string, string> AdditionalArguments { get; set; }
    }
}
