using Newtonsoft.Json;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class VcpkgDefinition
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "version")]
        public string Version { get; set; }

        [JsonProperty(PropertyName = "builtin-baseline")]
        public string Baseline { get; set; }

        [JsonProperty(PropertyName = "dependencies")]
        public List<VcpkgDependency> Dependencies { get; set; }

        [JsonProperty(PropertyName = "overrides")]
        public List<VcpkgDependency> Overrides { get; set; }

    }
}
