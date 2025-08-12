using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeOwnerValidatorHelper
    {
        /// <summary>
        /// Validates if a GitHub user meets the requirements to be an Azure SDK code owner.
        /// </summary>
        /// <param name="username">The GitHub username to validate</param>
        /// <param name="verbose">Whether to include verbose output</param>
        /// <returns>Validation result with detailed information</returns>
        Task<CodeOwnerValidationResult> ValidateCodeOwnerAsync(string username, bool verbose = false);
    }

    /// <summary>
    /// Validates GitHub users for Azure SDK code owner requirements.
    /// This is a C# replacement for the Validate-AzsdkCodeOwner.ps1 PowerShell script.
    /// </summary>
    [Description("Validates GitHub users for Azure SDK code owner requirements")]
    public class CodeOwnerValidatorHelper : ICodeOwnerValidatorHelper
    {
        private readonly IGitHubService githubService;
        private readonly ILogger<CodeOwnerValidatorHelper> logger;

        private static readonly HashSet<string> RequiredOrganizations = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft",
            "Azure"
        };

        public CodeOwnerValidatorHelper(IGitHubService _githubService, ILogger<CodeOwnerValidatorHelper> _logger)
        {
            githubService = _githubService;
            logger = _logger;
        }

        /// <summary>
        /// Validates if a GitHub user meets the requirements to be an Azure SDK code owner.
        /// </summary>
        /// <param name="username">The GitHub username to validate</param>
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
                var hasRequiredOrgs = await ValidateOrganizationsAsync(username, result);

                // Validate write permissions on azure-sdk-for-net
                var hasWritePermission = await ValidatePermissionsAsync(username);
                result.HasWritePermission = hasWritePermission;

                result.IsValidCodeOwner = hasRequiredOrgs && hasWritePermission;
                result.Status = "Success";
                result.Message = result.IsValidCodeOwner
                    ? "Valid code owner"
                    : "Not a valid code owner";

                return result;
            }
            catch (NotFoundException)
            {
                result.Status = "Error";
                result.Message = $"GitHub user '{username}' not found";
                logger.LogWarning("GitHub user not found: {Username}", username);
                return result;
            }
            catch (RateLimitExceededException ex)
            {
                result.Status = "Error";
                result.Message = $"Rate limit exceeded. Reset at: {ex.Reset}";
                logger.LogError("Rate limit exceeded for user {Username}. Reset at: {Reset}", 
                    username, ex.Reset);
                return result;
            }
            catch (SecondaryRateLimitExceededException ex)
            {
                result.Status = "Error";
                result.Message = $"Secondary rate limit exceeded. Please wait a few minutes before trying again.";
                logger.LogError("Secondary rate limit exceeded for user {Username}: {Message}", 
                    username, ex.Message);
                return result;
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Message = $"Error validating user: {ex.Message}";
                logger.LogError(ex, "Error validating GitHub user: {Username}", username);
                return result;
            }
        }

        /// <summary>
        /// Validates the user's organization memberships (Microsoft and Azure only).
        /// </summary>
        private async Task<bool> ValidateOrganizationsAsync(string username, CodeOwnerValidationResult result)
        {
            try
            {
                var client = ((GitHubService)githubService).gitHubClient;
                var organizations = await client.Organization.GetAllForUser(username);
                var userOrgs = organizations.Select(org => org.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Check only required organizations
                foreach (var requiredOrg in RequiredOrganizations)
                {
                    bool isMember = userOrgs.Contains(requiredOrg);
                    result.Organizations[requiredOrg] = isMember;
                }

                return RequiredOrganizations.All(org => result.Organizations[org]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validating organizations for user: {Username}", username);
                throw;
            }
        }

        /// <summary>
        /// Validates the user's write permissions on azure-sdk-for-net repository.
        /// </summary>
        private async Task<bool> ValidatePermissionsAsync(string username)
        {
            try
            {
                var client = ((GitHubService)githubService).gitHubClient;
                
                try
                {
                    var permission = await client.Repository.Collaborator.ReviewPermission("Azure", "azure-sdk-for-net", username);
                    return permission.Permission.Equals("write", StringComparison.OrdinalIgnoreCase) || 
                           permission.Permission.Equals("admin", StringComparison.OrdinalIgnoreCase);
                }
                catch (NotFoundException)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validating permissions for user: {Username}", username);
                throw;
            }
        }
    }

}
