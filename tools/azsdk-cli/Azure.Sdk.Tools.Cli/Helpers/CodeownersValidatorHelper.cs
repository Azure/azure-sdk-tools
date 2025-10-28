using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface ICodeownersValidatorHelper
    {
        /// <summary>
        /// Validates if a GitHub user meets the requirements to be an Azure SDK code owner.
        /// </summary>
        /// <param name="username">The GitHub username to validate</param>
        /// <param name="verbose">Whether to include verbose output</param>
        /// <returns>Validation result with detailed information</returns>
        Task<CodeownersValidationResult> ValidateCodeOwnerAsync(string username, bool verbose = false);
    }

    /// <summary>
    /// Validates GitHub users for Azure SDK code owner requirements.
    /// This is a C# replacement for the Validate-AzsdkCodeOwner.ps1 PowerShell script.
    /// </summary>
    [Description("Validates GitHub users for Azure SDK code owner requirements")]
      public class CodeownersValidatorHelper(IGitHubService githubService) : ICodeownersValidatorHelper
    {
        private static readonly HashSet<string> RequiredOrganizations = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft",
            "Azure"
        };
        
        /// <summary>
        /// Validates if a GitHub user meets the requirements to be an Azure SDK code owner.
        /// </summary>
        /// <param name="username">The GitHub username to validate</param>
        /// <returns>Validation result with detailed information</returns>
        public async Task<CodeownersValidationResult> ValidateCodeOwnerAsync(string username, bool verbose = false)
        {
            try
            {
                // Get user's public organization memberships and evaluate required-org membership inline
                var memberships = await githubService.GetPublicOrgMembership(username);
                var hasRequiredOrgs = memberships != null && RequiredOrganizations.All(o => memberships.Contains(o));

                // Populate result. Organizations from memberships without having the validator helper mutate the result.
                Dictionary<string, bool> organizations = new();
                foreach (var requiredOrg in RequiredOrganizations)
                {
                    organizations[requiredOrg] = memberships?.Contains(requiredOrg) ?? false;
                }

                // Validate write permissions on azure-sdk-for-net
                var hasWritePermission = await githubService.HasWritePermission("Azure", "azure-sdk-for-net", username);

                var isValidCodeowner = hasRequiredOrgs && hasWritePermission;
                return new CodeownersValidationResult
                {
                    Username = username,
                    Status = "Success",
                    Message = isValidCodeowner
                        ? "Valid code owner" 
                        : "Not a valid code owner",
                    Organizations = organizations,
                    HasWritePermission = hasWritePermission,
                    IsValidCodeOwner = isValidCodeowner,
                };
            }
            catch (NotFoundException)
            {
                return new CodeownersValidationResult
                {
                    Username = username,
                    Status = "Error",
                    Message = $"GitHub user not found '{username}' not found"
                };
            }
            catch (RateLimitExceededException ex)
            {
                return new CodeownersValidationResult
                {
                    Username = username,
                    Status = "Error",
                    Message = $"Rate limit exceeded for user {username}. Reset at: {ex.Reset}"
                };
            }
            catch (SecondaryRateLimitExceededException ex)
            {
                return new CodeownersValidationResult
                {
                    Username = username,
                    Status = "Error",
                    Message = $"Secondary rate limit exceeded for user {username}: {ex.Message}."
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error validating user {username}: {ex.Message}");
            }
        }
    }

}
