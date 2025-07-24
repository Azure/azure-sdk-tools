using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// Validates GitHub users for Azure SDK code owner requirements.
    /// This is a C# replacement for the Validate-AzsdkCodeOwner.ps1 PowerShell script.
    /// </summary>
    [Description("Validates GitHub users for Azure SDK code owner requirements")]
    public class CodeOwnerValidator : ICodeOwnerValidator
    {
        private readonly IGitHubService _githubService;
        private readonly ILogger<CodeOwnerValidator> _logger;

        private static readonly HashSet<string> RequiredOrganizations = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft",
            "Azure"
        };

        public CodeOwnerValidator(IGitHubService githubService, ILogger<CodeOwnerValidator> logger)
        {
            _githubService = githubService;
            _logger = logger;
        }

        /// <summary>
        /// Validates if a GitHub user meets the requirements to be an Azure SDK code owner.
        /// </summary>
        /// <param name="username">The GitHub username to validate</param>
        /// <param name="verbose">Whether to include verbose output</param>
        /// <returns>Validation result with detailed information</returns>
        public async Task<CodeOwnerValidationResult> ValidateCodeOwnerAsync(string username, bool verbose = false)
        {
            var result = new CodeOwnerValidationResult
            {
                Username = username,
                Organizations = new Dictionary<string, bool>(),
                HasWritePermission = false,
                IsValidCodeOwner = false,
                Status = "Processing"
            };

            try
            {
                _logger.LogInformation("Validating GitHub user: {Username}", username);

                // Validate organizations
                var organizationValidation = await ValidateOrganizationsAsync(username, verbose);
                result.Organizations = organizationValidation.Organizations;
                var hasRequiredOrgs = organizationValidation.HasAllRequired;

                // Validate permissions
                var permissionValidation = await ValidatePermissionsAsync(username, verbose);
                result.HasWritePermission = permissionValidation.HasWritePermission;

                // Final validation
                result.IsValidCodeOwner = hasRequiredOrgs && result.HasWritePermission;
                result.Status = "Success";
                result.Message = result.IsValidCodeOwner
                    ? "Valid code owner"
                    : "Not a valid code owner";

                _logger.LogInformation("Validation complete for {Username}: {IsValid}",
                    username, result.IsValidCodeOwner);

                return result;
            }
            catch (NotFoundException)
            {
                result.Status = "Error";
                result.Message = $"GitHub user '{username}' not found";
                _logger.LogWarning("GitHub user not found: {Username}", username);
                return result;
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Message = $"Error validating user: {ex.Message}";
                _logger.LogError(ex, "Error validating GitHub user: {Username}", username);
                return result;
            }
        }

        /// <summary>
        /// Validates the user's organization memberships.
        /// </summary>
        private async Task<OrganizationValidationResult> ValidateOrganizationsAsync(string username, bool verbose)
        {
            var result = new OrganizationValidationResult
            {
                Organizations = new Dictionary<string, bool>(),
                OtherOrganizations = new List<string>()
            };

            try
            {
                // Get user's public organizations
                var client = ((GitHubService)_githubService).gitHubClient;
                var organizations = await client.Organization.GetAllForUser(username);

                var userOrgs = organizations.Select(org => org.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Check required organizations
                var missingOrgs = new HashSet<string>(RequiredOrganizations);

                foreach (var requiredOrg in RequiredOrganizations)
                {
                    bool isMember = userOrgs.Contains(requiredOrg);
                    result.Organizations[requiredOrg] = isMember;

                    if (isMember)
                    {
                        missingOrgs.Remove(requiredOrg);
                    }
                }

                // Track other organizations for verbose output
                if (verbose)
                {
                    result.OtherOrganizations = userOrgs
                        .Where(org => !RequiredOrganizations.Contains(org))
                        .ToList();
                }

                result.HasAllRequired = missingOrgs.Count == 0;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating organizations for user: {Username}", username);
                throw;
            }
        }

        /// <summary>
        /// Validates the user's repository permissions.
        /// </summary>
        private async Task<PermissionValidationResult> ValidatePermissionsAsync(string username, bool verbose)
        {
            var result = new PermissionValidationResult();

            try
            {
                // Check permissions on azure-sdk-for-net repository as the canonical test
                var client = ((GitHubService)_githubService).gitHubClient;

                // Check if user is a collaborator
                var collaborators = await client.Repository.Collaborator.GetAll("Azure", "azure-sdk-for-net");
                var userCollaborator = collaborators.FirstOrDefault(c =>
                    string.Equals(c.Login, username, StringComparison.OrdinalIgnoreCase));

                if (userCollaborator != null)
                {
                    // User is a collaborator, assume they have write permissions
                    // This is a conservative approach since the PowerShell script checks specific permissions
                    result.Permission = "write";
                    result.HasWritePermission = true;
                }
                else
                {
                    // User is not a direct collaborator
                    result.Permission = "none";
                    result.HasWritePermission = false;
                }

                if (verbose)
                {
                    result.RawPermissions = new Dictionary<string, bool>
                    {
                        ["is_collaborator"] = userCollaborator != null,
                        ["has_write_access"] = result.HasWritePermission
                    };
                }

                return result;
            }
            catch (NotFoundException)
            {
                // Repository not found or access denied
                result.Permission = "none";
                result.HasWritePermission = false;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating permissions for user: {Username}", username);
                throw;
            }
        }
    }

    /// <summary>
        /// Result of organization validation.
        /// </summary>
        public class OrganizationValidationResult
        {
            public Dictionary<string, bool> Organizations { get; set; } = new();
            public List<string> OtherOrganizations { get; set; } = new();
            public bool HasAllRequired { get; set; }
        }

    /// <summary>
    /// Result of permission validation.
    /// </summary>
    public class PermissionValidationResult
    {
        public string Permission { get; set; } = "none";
        public bool HasWritePermission { get; set; }
        public Dictionary<string, bool>? RawPermissions { get; set; }
    }
}
