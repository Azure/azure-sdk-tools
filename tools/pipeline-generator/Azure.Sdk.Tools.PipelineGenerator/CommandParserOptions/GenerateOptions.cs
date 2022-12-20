using CommandLine;
using System.Collections.Generic;

namespace PipelineGenerator.CommandParserOptions
{
    [Verb("generate", HelpText = "Generate Azure Pipelines from config files")]
    public class GenerateOptions : DefaultOptions
    {
        [Option('x', "prefix", Required = true, HelpText = "The prefix to append to the pipeline name")]
        public string Prefix { get; set; }

        [Option("path", Required = true, HelpText = "The directory from which to scan for components")]
        public string Path { get; set; }

        [Option('d', "devopspath", Required = false, HelpText = "The DevOps directory for created pipelines")]
        public string DevOpsPath { get; set; }

        [Option('e', "endpoint", Required = false, Default = "Azure", HelpText = "Name of the service endpoint to configure repositories with. Default: Azure")]
        public string Endpoint { get; set; }

        [Option('r', "repository", Required = true, HelpText = "Name of the GitHub repo in the form [org]/[repo]")]
        public string Repository { get; set; }

        [Option('b', "branch", Required = false, Default = "refs/heads/main", HelpText = "Default: refs/heads/main")]
        public string Branch { get; set; }

        [Option('a', "agentpool", Required = false, Default = "Hosted", HelpText = "Name of the agent pool to use when pool isn't specified. Default: hosted")]
        public string Agentpool { get; set; }

        [Option('c', "convention", Required = true, HelpText = "The convention to build pipelines for: [ci|up|upweekly|tests|testsweekly]")]
        public string Convention { get; set; }

        [Option('v', "variablegroups", Required = false, HelpText = "Variable groups to link, separated by a space, e.g. --variablegroups 1 9 64")]
        public IEnumerable<int> VariableGroups { get; set; }

        [Option("open", Required = false, HelpText = "Open a browser window to the definitions that are created")]
        public bool Open { get; set; }

        [Option("destroy", Required = false, HelpText = "Use this switch to delete the pipelines instead (DANGER!)")]
        public bool Destroy { get; set; }

        [Option("debug", Required = false, HelpText = "Turn on debug level logging")]
        public bool Debug { get; set; }

        [Option("no-schedule", Required = false, HelpText = "Skip creating any scheduled triggers")]
        public bool NoSchedule { get; set; }

        [Option("set-managed-variables", Required = false, HelpText = "Set managed meta.* variable values")]
        public bool SetManagedVariables { get; set; }

        [Option("overwrite-triggers", Required = false, HelpText = "Overwrite existing pipeline triggers (triggers may be manually modified, use with caution)")]
        public bool OverwriteTriggers { get; set; }
    }
}
