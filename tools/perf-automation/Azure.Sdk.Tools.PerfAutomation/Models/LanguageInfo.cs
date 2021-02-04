using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class LanguageInfo
    {
        public string Project { get; set; }
        public IEnumerable<IDictionary<string, string>> PackageVersions { get; set; }
        public string AdditionalArguments { get; set; }
    }

}
