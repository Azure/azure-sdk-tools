using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Tools.Package;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package
{
    public class SdkBreakingChangeDetectResponse: PackageResponseBase
    {
        [JsonPropertyName("breakingChanges")]
        public SdkBreakingChange[] BreakingChanges { get; set; }
        [JsonPropertyName("hasBreakingChanges")]
        public bool HasBreakingChanges { get; set; }
        [JsonPropertyName("SdkChangesMd")]
        public string? SdkChangesMd { get; set; } = null;

        protected override string Format()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Has Breaking Changes: {HasBreakingChanges}");
            sb.AppendLine();
            sb.AppendLine($"Language: {Language}");
            sb.AppendLine();
            if (BreakingChanges == null || BreakingChanges.Length == 0)
            {
                sb.AppendLine("No breaking changes detected.");
            } else
            {
                sb.AppendLine("**Breaking Changes:**");
                foreach (var change in BreakingChanges)
                {
                    sb.AppendLine($"- Change: {change.BreakingChange}");
                    sb.AppendLine($"  Category: {change.Category}");
                    if (!string.IsNullOrEmpty(change.Resolution))
                    {
                        sb.AppendLine($"  Resolution: {change.Resolution}");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }
}
