using System.Collections.Generic;
using System.Text.Json;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class SanitizerBody {
        public string Name { get; set; }
        public JsonDocument Body { get; set; }
    }
}
