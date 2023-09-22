using CommandLine;

namespace PipelineGenerator.CommandParserOptions
{
    public class DefaultOptions
    {
        private string _organization = "";

        [Option('o', "organization", Required = false, Default = "azure-sdk", HelpText = "Azure DevOps organization name. Default: azure-sdk")]
        public string Organization
        {
            get { return _organization; }
            set {
                if (_organization.StartsWith("https://dev.azure.com/"))
                {
                    _organization = value;
                }
                else
                {
                    _organization = "https://dev.azure.com/" + value;
                }
            }
        }

        [Option('p', "project", Required = false, Default = "internal", HelpText = "Azure DevOps project name. Default: internal")]
        public string Project { get; set; }

        [Option('t', "patvar", Required = false, HelpText = "Environment variable name containing a Personal Access Token.")]
        public string Patvar { get; set; }

        [Option("whatif", Required = false, HelpText = "Dry Run changes")]
        public bool WhatIf { get; set; }
    }
}