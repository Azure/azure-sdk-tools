using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    public class Language
    {
        public LanguageName Name { get; set; }
        public string Project { get; set; }
        public string TestName { get; set; }
        public IEnumerable<IDictionary<string, string>> PackageVersions { get; set; }
    }
}
