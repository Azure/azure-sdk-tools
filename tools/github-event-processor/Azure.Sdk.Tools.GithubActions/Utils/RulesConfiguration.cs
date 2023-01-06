using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Azure.Sdk.Tools.GithubEventProcessor.Program;

namespace Azure.Sdk.Tools.GithubEventProcessor.Utils
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    // These would be enum names that match the rule names
    public enum RuleEnum
    {
        Rule1,
        Rule2,
        Rule3,
        Rule4
    }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuleState
    {
        On,
        Off
    }

    public class Rule
    {
        public Rule() { }

        public Rule(RuleEnum ruleName, RuleState state)
        {
            RuleName = ruleName;
            State = state;
        }

        public RuleEnum RuleName { get; set; }
        public RuleState State { get; set; }
    }
    public class RulesConfiguration
    {
        List<Rule> Rules { get; set; }
        public RulesConfiguration() 
        {
            Rules = new List<Rule>();
        }
        public void TestIt()
        {
            Rules.Add(new Rule(RuleEnum.Rule1, RuleState.On));
            Rules.Add(new Rule(RuleEnum.Rule2, RuleState.Off));
            Rules.Add(new Rule(RuleEnum.Rule3, RuleState.On));
            Rules.Add(new Rule(RuleEnum.Rule4, RuleState.Off));
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(Rules, options);
            Console.WriteLine(jsonString);

            List<Rule> Rules2 = JsonSerializer.Deserialize<List<Rule>>(jsonString);
            Console.WriteLine(Rules2);
        }
    }


    // JRS - This does not work, it won't serialize, it'll throw the following exception
    // The collection type 'System.Collections.Generic.Dictionary`2[Azure.Sdk.Tools.GithubEventProcessor.Utils.RuleEnum,Azure.Sdk.Tools.GithubEventProcessor.Utils.RuleState]' is not supported.
    public class RulesConfiguration2
    {
        public Dictionary<RuleEnum, RuleState> Rules { get; set; }
        public RulesConfiguration2()
        {
            Rules = new Dictionary<RuleEnum, RuleState>();
        }
        public void TestIt()
        {
            Rules.Add(RuleEnum.Rule1, RuleState.On);
            Rules.Add(RuleEnum.Rule2, RuleState.Off);
            Rules.Add(RuleEnum.Rule3, RuleState.On);
            Rules.Add(RuleEnum.Rule4, RuleState.Off);
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(Rules, options);
            Console.WriteLine(jsonString);

            Dictionary<RuleEnum, RuleState> Rules2 = JsonSerializer.Deserialize<Dictionary<RuleEnum, RuleState>>(jsonString);
            Console.WriteLine(Rules2);
        }
    }
}
