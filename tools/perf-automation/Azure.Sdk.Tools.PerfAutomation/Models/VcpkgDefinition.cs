using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
