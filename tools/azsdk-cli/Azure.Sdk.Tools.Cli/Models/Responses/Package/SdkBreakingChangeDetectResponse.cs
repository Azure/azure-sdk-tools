using System.Text;
using Azure.Sdk.Tools.Cli.Tools.Package;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package
{
    public class SdkBreakingChangeDetectResponse: PackageResponseBase
    {
        public SdkBreakingChange[] BreakingChanges { get; set; }
        public bool HasBreakingChanges;

        protected override string Format()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Has Breaking Changes: {HasBreakingChanges}");
            sb.AppendLine();
            sb.AppendLine($"Language: {Language}");
            sb.AppendLine();
            sb.AppendLine("**Breaking Changes:**");
            foreach (var change in BreakingChanges)
            {
                sb.AppendLine($"- Change: {change.BreakingChange}");
                sb.AppendLine($"  Category: {change.Category}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
