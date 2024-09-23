using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using System.IO;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Utils
{
    /// <summary>
    /// Enum for whether or not a given rule is enabled (On) or disabled (Off).
    /// JsonStringEnumConverter is necessary so the rules, when stored in the file, show On/Off instead
    /// of 0/1 which makes things more readable.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuleState
    {
        On,
        Off
    }

    /// <summary>
    /// Class to hold the RulesConfiguration which includes methods to load the config, check whether
    /// a given rule is enabled/disabled and report any missing rules.
    /// </summary>
    public class RulesConfiguration
    {
        public static readonly string RulesConfigFileName = "event-processor.config";
        public static readonly string RulesConfigSubDirectory = ".github";
        public static string rulesConfigFilePathOverride = null;
        public Dictionary<string, RuleState> Rules { get; set; }
        public string RulesConfigFile { get; set; } = null;
        public RulesConfiguration() 
        {
            Rules = new Dictionary<string, RuleState>();
        }

        /// <summary>
        /// Overloaded constructor. If the configuration file location is passed in, load that otherwise
        /// look in a well known location for the repository config file.
        /// </summary>
        /// <param name="configurationFile">Location of the configuration file or null, to look in the well known location for a repository.</param>
        public RulesConfiguration(string configurationFile = null)
        {
            Rules = new Dictionary<string, RuleState>();
            string configLoc = configurationFile;
            if (configLoc == null)
            {
                if (null != rulesConfigFilePathOverride)
                {
                    // If the user overrode the location
                    configLoc = rulesConfigFilePathOverride;
                }
                else
                {
                    // Load the config from the well known location, somewhere under the .github directory
                    // which is in the root of the repository
                    configLoc = DirectoryUtils.FindFileInRepository(RulesConfigFileName, RulesConfigSubDirectory);
                }
            }
            RulesConfigFile = configLoc;
            LoadRulesFromConfig();
        }

        /// <summary>
        /// Load the rules from the configuration. If there is no rules configuration file set
        /// then create an in memory config file with the rules defaulting to Off.
        /// </summary>
        public void LoadRulesFromConfig()
        {
            if (null != RulesConfigFile)
            {
                string rawJson = FileHelpers.GetFileOrUrlContents(RulesConfigFile);
                Console.WriteLine($"Loading repository rules from {RulesConfigFile}");
                Rules = JsonSerializer.Deserialize<Dictionary<string, RuleState>>(rawJson);
                // Report any rules that might be missing from the config file.
                ReportMissingRulesFromConfig();
            }
            else
            {
                // If there is no rules config in the repository, create an in memory config
                // with all of the rules set to Off
                Console.WriteLine("The rules configuration file was null which means it wasn't passed in or able to be discovered within the repository. A default in memory config will be created with all of the rules set to Off.");
                CreateDefaultConfig(RuleState.Off);
            }
        }

        /// <summary>
        /// Check whether or not a given rule is enabled, disabled or missing.
        /// </summary>
        /// <param name="rule">String, rule to check. This string should be from RulesConstants.</param>
        /// <param name="outputRunLogMessages">Boolean to determine whether or not to put log messages, default is true. This is necessary because we have a case where one rule runs another's processing but we only want the log outputting when the specific rule is being executed to avoid confusion.</param>
        /// <returns>True if enabled, False if disabled or missing from the configuration file.</returns>
        public bool RuleEnabled(string rule, bool outputRunLogMessages = true)
        {
            if (Rules.ContainsKey(rule))
            {
                if (Rules[rule] == RuleState.On)
                {
                    if (outputRunLogMessages)
                    {
                        Console.WriteLine($"Rule, {rule}, is {Rules[rule]}. Processing...");
                    }
                    return true;
                }

                if (outputRunLogMessages)
                {
                    Console.WriteLine($"Rule, {rule}, is {Rules[rule]}. Not processing...");
                }
            }
            else
            {
                if (outputRunLogMessages)
                {
                    Console.WriteLine($"Rule, {rule}, is not in the repository config file and will not run.");
                }
            }
            return false;
        }

        /// <summary>
        /// Load up all of the rules from the RulesConstants class and compare those with the rules
        /// loaded from the config file. Report any rules that are not in the config file.
        /// </summary>
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
                    Console.WriteLine($"{rule} was not in the rules config file. Missing rules default to Off.");
                }
            }
        }

        /// <summary>
        /// Creates a default configuration, in memory, with all rule values set to the defaultState which
        /// defaults to RuleState.Off
        /// </summary>
        /// <param name="defaultState">RuleState enum, default to on</param>
        public void CreateDefaultConfig(RuleState defaultState = RuleState.Off)
        {
            var rules = typeof(RulesConstants)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Where(field => field.FieldType == typeof(String))
                .Select(field => field.GetValue(null) as String);
            foreach (string rule in rules)
            {
                Rules.Add(rule, defaultState);
            }
        }
    }
}
