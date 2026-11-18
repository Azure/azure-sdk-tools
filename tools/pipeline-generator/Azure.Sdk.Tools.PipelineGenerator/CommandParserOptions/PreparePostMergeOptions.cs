using CommandLine;

namespace PipelineGenerator.CommandParserOptions
{
    [Verb("prepare-post-merge", HelpText = "Prepare merged SDK descriptors and submit their release runs from the trusted tools pipeline.")]
    public class PreparePostMergeOptions
    {
        [Option("repository-name", Default = "azure-sdk-for-net", HelpText = "Allowlisted SDK basename, without Azure/.")]
        public string RepositoryName { get; set; } = "azure-sdk-for-net";

        [Option("merge-commit-sha", Required = true, HelpText = "Full merged SDK commit SHA in default-branch history.")]
        public string MergeCommitSha { get; set; }

        [Option("descriptor-paths", Required = true, HelpText = "JSON array of {path, previousPath?} targets.")]
        public string DescriptorPaths { get; set; }

        [Option("sdk-directory", Required = true, HelpText = "Existing local SDK root, sparse-checked out and validated at the merged SHA by the trusted pipeline. No Git CLI is used by this command.")]
        public string SdkDirectory { get; set; }

        [Option("results-directory", Required = true, HelpText = "Directory to publish as the release-run-results Build artifact.")]
        public string ResultsDirectory { get; set; }
    }
}
