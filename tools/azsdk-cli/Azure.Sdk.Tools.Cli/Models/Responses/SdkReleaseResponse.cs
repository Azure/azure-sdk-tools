using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses
{
    public class SdkReleaseResponse : CommandResponse
    {
        [JsonPropertyName("Package name")]
        public string PackageName { get; set; } = string.Empty;

        [JsonPropertyName("Version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("Release pipeline URL")]
        public string ReleasePipelineRunUrl { get; set; } = string.Empty;

        [JsonPropertyName("Pipeline build id")]
        public int PipelineBuildId { get; set; }

        [JsonPropertyName("Release status")]
        public string ReleasePipelineStatus { get; set; } = string.Empty;

        [JsonPropertyName("Release status details")]
        public string ReleaseStatusDetails { get; set; } = string.Empty;

        public override string ToString()
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
            return ToString(output.ToString());
        }
    }
}
