using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Errors;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.CodeownersUtils.Verification
{
    /// <summary>
    /// Verification class for CODEOWNERS lines containing owners.
    /// </summary>
    public static class Owners
    {
        /// <summary>
        /// Verify the owners (teams or users) for a given CODEOWNERS line
        /// </summary>
        /// <param name="ownerData">OwnerData instance</param>
        /// <param name="line">The CODEOWNERS line being parsed</param>
        /// <param name="expectOwners">Whether or not owners are expected. Some monikers may or may not have owners if their block ends in a source path/owner line.</param>
        /// <param name="errorStrings">List of errors belonging to the current line. New errors are added to the list.</param>
        public static void VerifyOwners(OwnerDataUtils ownerData, string line, bool expectOwners, List<string> errorStrings)
        {
            List<string> ownerList = ParsingUtils.ParseOwnersFromLine(ownerData, line, false /* teams aren't expanded for linting */);
            // Some CODEOWNERS lines require owners to be on the line, like source path/owners lines. Some CODEOWNERS
            // monikers, eg. AzureSdkOwners, may or may not have owners depending on whether or not the block they're
            // part of ends in a source path/owners line. If the line doesn't contain the Owner Separator and owners
            // are expected, then add the error and return, otherwise just return.
            if (ownerList.Count == 0)
            {
                if (expectOwners)
                {
                    errorStrings.Add(ErrorMessageConstants.NoOwnersDefined);
                }
                return;
            }

            // Verify that each owner exists and has write permissions
            foreach (string owner in ownerList)
            {
                // If the owner is a team then it needs to have write permission and needs to be
                // one of the teams in the teamUser data. This is the same for metadata lines or
                // path lines in the CODEOWNERS file
                if (owner.StartsWith(OrgConstants.AzureOrgTeamConstant, StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure the team has write permission
                    if (!ownerData.IsWriteTeam(owner))
                    {
                        errorStrings.Add($"{owner}{ErrorMessageConstants.InvalidTeamPartial}");
                    }
                }
                // else, the owner is a user or a malformed team entry
                else
                {
                    // The list of sourcepath line owners comes directly from the CODEOWNERS errors which means the only owners being processed
                    // are ones that have already been flagged as errors. This means that only the following checks need to be done.
                    // 1. If an owner has write permission and but isn't a member of Azure, it means the owner's membership in Azure isn't public
                    //    and needs to be.
                    // 2. If the owner doesn't have write permission:
                    //    a. Check whether or not the owner is a malformed team entry (an entry that doesn't start with @Azure/)
                    // If the owner has write permission
                    if (ownerData.IsWriteOwner(owner))
                    {
                        // Verify that the owner is a public member of Azure. Github's parsing of CODEOWNERS doesn't recognize
                        // an owner unless their azure membership is public
                        if (!ownerData.IsPublicAzureMember(owner))
                        {
                            errorStrings.Add($"{owner}{ErrorMessageConstants.NotAPublicMemberOfAzurePartial}");
                        }
                    }
                    else
                    {
                        // if the owner is not a write user, check and see if the entry is a malformed team entry, missing the "@Azure/"
                        if (ownerData.IsWriteTeam(owner))
                        {
                            errorStrings.Add($"{owner}{ErrorMessageConstants.MalformedTeamEntryPartial}");
                        }
                        else
                        {
                            // At this point whomever they are they're not:
                            // 1. a team under azure-sdk-write
                            // 2. a malformed team entry, for a team under azure-sdk-write
                            // 3. a user with write permissions (would be part of the distinct list of all users from all
                            //    teams under azure-sdk-write)
                            // It's unclear, with the data we have, if they're even a valid GitHub user and, if so, if
                            // they're even part of Azure.
                            errorStrings.Add($"{owner}{ErrorMessageConstants.InvalidUserPartial}");
                        }
                    }
                }
            }
        }
    }
}
