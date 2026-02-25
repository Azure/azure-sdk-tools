using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package
{
    public class SdkReleaseResponse : PackageResponseBase
    {
        [JsonPropertyName("Release pipeline URL")]
        public string ReleasePipelineRunUrl { get; set; } = string.Empty;

        [JsonPropertyName("Pipeline build id")]
        public int PipelineBuildId { get; set; }

        [JsonPropertyName("Release status")]
        public string ReleasePipelineStatus { get; set; } = string.Empty;

        [JsonPropertyName("Release status details")]
        public string ReleaseStatusDetails { get; set; } = string.Empty;

        protected override string Format()
        {
            //Create an output string with all the properties of the package release
            StringBuilder output = new StringBuilder();
            output.AppendLine($"### Package Name: {PackageName}");
            output.AppendLine($"### Version: {Version}");
            output.AppendLine($"### Language: {Language}");
            output.AppendLine($"### Release Pipeline Run: {ReleasePipelineRunUrl}");
            output.AppendLine($"### Release Build Id: {PipelineBuildId}");
            output.AppendLine($"### Release Pipeline Status: {ReleasePipelineStatus}");
            output.AppendLine($"### Release Status Details: {ReleaseStatusDetails}");
            return output.ToString();
        }
    }
}
