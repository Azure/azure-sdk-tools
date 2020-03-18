using System.Collections.Generic;

namespace CreateRuleFabricBot.Rules.IssueRouting
{
    public class TriageConfig
    {
        public List<string> Labels { get; } = new List<string>();
        public List<string> Mentionee { get; } = new List<string>();

        public override string ToString()
        {
            string labels = "\"" + string.Join("\",\"", Labels) + "\"";
            string mentionee = "\"" + string.Join("\",\"", Mentionee) + "\"";

            return $"{{ " +
                $"\"labels\": [  {labels}  ], " +
                $"\"mentionees\": [ {mentionee}  ]" +
                $" }}";
        }
    }
}
