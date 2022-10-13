using Newtonsoft.Json;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.PerfAutomation.Models
{
    // common for both dependency and override
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class VcpkgDependency
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "version")]
        public string Version { get; set; }

        [JsonProperty(PropertyName = "features")]
        public List<string> Features { get; set; }

        [JsonProperty(PropertyName = "platform")]
        public string Platform { get; set; }

        [JsonProperty(PropertyName = "version>=")]
        public string VersionGt { get; set; }
    }
}
