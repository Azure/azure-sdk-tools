using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeOwnerValidationHelper
    {
        CodeownersEntry? FindServiceEntries(IList<CodeownersEntry> entries, string serviceName);
        List<string> ExtractUniqueOwners(CodeownersEntry entry);
    }

    public class CodeOwnerValidationHelper : ICodeOwnerValidationHelper
    {
        public CodeownersEntry? FindServiceEntries(IList<CodeownersEntry> entries, string serviceName)
        {
            foreach (var entry in entries)
            {
                // Check if service matches by ServiceLabels
                if (entry.ServiceLabels?.Any(label => label.Contains(serviceName, StringComparison.OrdinalIgnoreCase)) == true)
                    return entry;

                // Check if service matches by PRLabels  
                if (entry.PRLabels?.Any(label => label.Contains(serviceName, StringComparison.OrdinalIgnoreCase)) == true)
                    return entry;

                // Check if service matches by PathExpression
                if (entry.PathExpression?.Contains(serviceName, StringComparison.OrdinalIgnoreCase) == true)
                    return entry;

                // Check if service matches by any owner team names
                var allOwners = ExtractUniqueOwners(entry);
                if (allOwners.Any(owner => owner.Contains(serviceName, StringComparison.OrdinalIgnoreCase)))
                    return entry;
            }

            return null;
        }

        public List<string> ExtractUniqueOwners(CodeownersEntry entry)
        {
            var allOwners = new List<string>();
            if (entry.SourceOwners?.Any() == true) allOwners.AddRange(entry.SourceOwners);
            if (entry.ServiceOwners?.Any() == true) allOwners.AddRange(entry.ServiceOwners);
            if (entry.AzureSdkOwners?.Any() == true) allOwners.AddRange(entry.AzureSdkOwners);

            return allOwners.Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
        }
    }

    // Data models - moved from CodeOwnerTools for better organization
    public class ServiceCodeOwnerResult
    {
        public string Repository { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public List<CodeOwnerValidationResult> CodeOwners { get; set; } = new();
    }

    public class CodeOwnerValidationResult
    {
        public string Username { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsValidCodeOwner { get; set; }
        public bool HasWritePermission { get; set; }
        public Dictionary<string, bool> Organizations { get; set; } = new();
    }
}
