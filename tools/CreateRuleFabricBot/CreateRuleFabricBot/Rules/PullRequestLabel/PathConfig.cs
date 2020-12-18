using System.Collections.Generic;

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
            return $"{{ " +
                $"\"labels\": [\"{Label}\"], " +
                $"\"pathFilter\": [\"{Path}\"], " +
                "\"exclude\": [ \"\" ] " +
                $" }}";
        }
    }
}
