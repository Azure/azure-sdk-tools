using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace CreateRuleFabricBot.Rules.IssueRouting
{
    public class TriageConfig
    {
        public List<string> Labels { get; } = new List<string>();
        public List<string> Mentionee { get; } = new List<string>();

        public override string ToString()
        {
            var array = Labels.Select(label => new JValue(label)).ToArray();
            var arr = new JArray(array);

            return GetJsonPayload().ToString();
        }

        public JObject GetJsonPayload()
        {
            return new JObject(
                new JProperty("labels",
                    new JArray(Labels.Select(label => new JValue(label)).ToArray())),
                new JProperty("mentionees",
                    new JArray(Mentionee.Select(ment => new JValue(ment)).ToArray())));
        }
    }
}
