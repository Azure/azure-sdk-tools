using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    // The JsonStringEnumConverter is necessary so the rules, when stored in the file, show On/Off instead
    // of 0/1 which makes things more readable.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuleState
    {
        On,
        Off
    }

    public class RulesConfiguration
    {
        public Dictionary<string, RuleState> Rules { get; set; }
        public string RulesConfigFile { get; set; } = null;
        public RulesConfiguration() 
        {
            Rules = new Dictionary<string, RuleState>();
        }
        public RulesConfiguration(string configurationFile = null)
        {
            Rules = new Dictionary<string, RuleState>();
            string configLoc = configurationFile;
            if (configLoc != null)
            {
                // Load the config from the well known location, somewhere under the .github directory
                // which is in the root of the repository
                configLoc = DirectoryUtils.FindFileInRepository("rules_config.json", ".github");
            }
            RulesConfigFile = configLoc;
            LoadRulesFromConfig();
        }
        public void LoadRulesFromConfig()
        {
            // JRS - Remove - there are no config files anywhere yet, just load up all the current rules
            // and set them to On for the moment
            if (null == RulesConfigFile)
            {
                var rules = typeof(RulesConstants)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.IsLiteral)
                    .Where(field => field.FieldType == typeof(String))
                    .Select(field => field.GetValue(null) as String);
                foreach (string rule in rules)
                {
                    Rules.Add(rule, RuleState.On);
                }
            }
        }

        public bool RuleEnabled(string rule)
        {
            if (Rules.ContainsKey(rule))
            {
                Console.WriteLine($"Rule, {rule}, is {Rules[rule]}.");
                if (Rules[rule] == RuleState.On)
                {
                    return true;
                }
            }
            else
            {
                Console.WriteLine($"Rule, {rule}, is not in the repository config file and will not run.");
            }
            return false;
        }

        public void ReportMissingRulesFromConfig()
        {
            // Load up the RulesConstants into a list
            var rules = typeof(RulesConstants)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Where(field => field.FieldType == typeof(String))
                .Select(field => field.GetValue(null) as String);
            foreach (string rule in rules)
            {
                if (!Rules.ContainsKey(rule))
                {
                    Console.WriteLine($"{rule} was not in the rules config file {RulesConfigFile}, defaulting rule to off");
                }
            }
        }

        /// <summary>
        /// Creates a default configuration, in memory, with all rule values set to the defaultState which
        /// defaults to RuleState.On
        /// </summary>
        /// <param name="defaultState">RuleState enum, default to on</param>
        public void CreateDefaultConfig(RuleState defaultState = RuleState.On)
        {
            var rules = typeof(RulesConstants)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Where(field => field.FieldType == typeof(String))
                .Select(field => field.GetValue(null) as String);
            foreach (string rule in rules)
            {
                Rules.Add(rule, RuleState.On);
            }
        }

        // JRS-Remove
        public void TestIt()
        {
            Rules.Add(RulesConstants.InitialIssueTriage, RuleState.On);
            Rules.Add(RulesConstants.ManualIssueTriage, RuleState.Off);
            Rules.Add(RulesConstants.ServiceAttention, RuleState.On);
            Rules.Add(RulesConstants.CXPAttention, RuleState.Off);
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(Rules, options);
            Console.WriteLine(jsonString);

            Dictionary<string, RuleState> Rules2 = JsonSerializer.Deserialize<Dictionary<string, RuleState>>(jsonString);
            Console.WriteLine(Rules2[RulesConstants.InitialIssueTriage]);
            ReportMissingRulesFromConfig();

            string fullFilePath = DirectoryUtils.FindFileInRepository("CODEOWNERS", ".github");
            Console.WriteLine(fullFilePath);
        }
    }
}
