using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using System.Collections.Concurrent;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeOwnerValidationHelper
    {
        CodeownersEntry? FindServiceEntries(IList<CodeownersEntry> entries, string serviceName);
        Dictionary<string, bool> ExtractOrganizationStatus(string powerShellOutput);
        bool ExtractWritePermission(string powerShellOutput);
        bool ExtractCodeOwnerValidity(string powerShellOutput);
        List<string> ExtractUniqueOwners(CodeownersEntry entry);
        CodeOwnerValidationResult ParsePowerShellOutput(string powerShellOutput, string username);
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

        public Dictionary<string, bool> ExtractOrganizationStatus(string powerShellOutput)
        {
            var organizationStatus = new Dictionary<string, bool>();
            
            if (string.IsNullOrEmpty(powerShellOutput))
                return organizationStatus;
                
            var lines = powerShellOutput.Split('\n');
            bool inOrgSection = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                
                var cleanLine = line.Trim().Replace("\r", "");
                
                if (string.IsNullOrEmpty(cleanLine)) continue;

                if (cleanLine.StartsWith("Required Org")) // Organization is spelled wrong in Validate-AzsdkCodeOwner.ps1 script.
                {
                    inOrgSection = true;
                    continue;
                }

                if (cleanLine.StartsWith("Required Permissions") || cleanLine.StartsWith("Validation result"))
                {
                    inOrgSection = false;
                    continue;
                }

                if (inOrgSection)
                {
                    var isSuccess = cleanLine.Contains("✓");
                    var orgName = cleanLine.Replace("✓", "").Replace("✗", "").Replace("\uFFFD", "").Trim();
                    if (!string.IsNullOrEmpty(orgName))
                    {
                        organizationStatus[orgName] = isSuccess;
                    }
                }
            }

            return organizationStatus;
        }

        public bool ExtractWritePermission(string powerShellOutput)
        {
            if (string.IsNullOrEmpty(powerShellOutput))
                return false;
                
            var lines = powerShellOutput.Split('\n');
            foreach (var line in lines)
            {
                var cleanLine = line.Trim().Replace("\r", "");
                if (cleanLine.Contains("write", StringComparison.OrdinalIgnoreCase) && 
                    (cleanLine.Contains("✓") || cleanLine.Contains("\uFFFD")))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ExtractCodeOwnerValidity(string powerShellOutput)
        {
            if (string.IsNullOrEmpty(powerShellOutput))
                return false;
                
            // Look for exact phrase "Valid code owner" (case-insensitive) but not "Invalid code owner"
            return powerShellOutput.Contains("Valid code owner", StringComparison.OrdinalIgnoreCase) &&
                   !powerShellOutput.Contains("Invalid code owner", StringComparison.OrdinalIgnoreCase);
        }

        public CodeOwnerValidationResult ParsePowerShellOutput(string powerShellOutput, string username)
        {
            var validationResult = new CodeOwnerValidationResult
            {
                Username = username,
                Status = "Processing"
            };

            if (string.IsNullOrEmpty(powerShellOutput))
            {
                validationResult.Status = "Error";
                validationResult.Message = "No output received from PowerShell script";
                return validationResult;
            }

            // Parse the PowerShell output
            var requiredOrganizations = new Dictionary<string, bool>
            {
                { "azure", false },
                { "microsoft", false }
            };
            
            var organizationStatus = ExtractOrganizationStatus(powerShellOutput);
            foreach (var org in organizationStatus)
            {
                if (requiredOrganizations.ContainsKey(org.Key.ToLower()))
                {
                    requiredOrganizations[org.Key.ToLower()] = org.Value;
                }
            }

            validationResult.Status = "Success";
            validationResult.Organizations = requiredOrganizations;
            validationResult.HasWritePermission = ExtractWritePermission(powerShellOutput);
            validationResult.IsValidCodeOwner = ExtractCodeOwnerValidity(powerShellOutput);

            return validationResult;
        }
    }

    // Data models - moved from CommonValidationTool for better organization
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
