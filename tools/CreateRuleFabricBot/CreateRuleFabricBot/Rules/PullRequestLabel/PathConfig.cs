using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CreateRuleFabricBot.Rules.PullRequestLabel
{
    public class PathConfig
    {
        public PathConfig(string pathExpression, string label)
        {
            // at this point we should remove the leading '/' if any
            if (pathExpression.StartsWith("/"))
            {
                pathExpression = pathExpression.Substring(1);
            }

            Path = pathExpression;
            Label = label;
        }

        public string Label { get; set; } = "";
        public string Path { get; set; } = "";

        public override string ToString()
        {
            return GetJsonPayload().ToString();
        }

        public JObject GetJsonPayload()
        {
            return new JObject(
                new JProperty("label", new JValue(Label)),
                new JProperty("pathFilter", new JArray(new JValue(Path))),
                new JProperty("exclude", new JArray(new JValue(""))));
            // Note: By using an empty string in the exclude property above, 
            // the rule we create will allow multiple labels (from different folders) to be applied to the same PR.
        }
    }
}
