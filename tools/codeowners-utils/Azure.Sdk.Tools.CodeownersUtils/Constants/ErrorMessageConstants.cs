using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Constants
{
    /// <summary>
    /// Class to store error message constant strings. Anything that's a partial needs to have something
    /// prepended to it. For example: The NoWritePermissionPartial is an Owner error and requires the 
    /// owner to be prepended whereas NeedsToEndWithSourceOwnerPartial is a moniker error and requires
    /// the moniker prepended
    /// </summary>
    public class ErrorMessageConstants
    {
        // Invalid CODEOWNERS path or file. This can happen if the repoRoot argument is incorrect or
        // if someone opted to try and sparse checkout the pipeline where this is running (it
        // cannot do all of the necessary checks in sparsely checked out repositor)y.
        public const string PathOrFileNotExistInRepoPartial = " path or file does not exist in repository.";

        // Owner errors
        public const string InvalidTeamPartial = " is an invalid team. Ensure the team exists and has write permissions.";
        public const string InvalidUserPartial = " is an invalid user. Ensure the user exists, is public member of Azure and has write permissions.";
        public const string MalformedTeamEntryPartial = " is a malformed team entry and should start with '@Azure/'.";
        public const string NoOwnersDefined = "There are no owners defined for CODEOWNERS entry.";
        public const string NotAPublicMemberOfAzurePartial = " is not a public member of Azure.";

        // Label errors
        public const string InvalidRepositoryLabelPartial = " is not a valid label for this repository.";
        public const string MissingLabelForMoniker = "Moniker requires a label entry.";
        public const string ServiceAttentionIsNotAValidPRLabel = $"{LabelConstants.ServiceAttention} is not a valid label for {MonikerConstants.PRLabel}";
        public const string ServiceLabelMustContainAServiceLabel = $"{MonikerConstants.ServiceLabel} is must contain a valid label, not just the {LabelConstants.ServiceAttention} label.";

        // Invalid CODEOWNERS source path messages
        public const string MustStartWithASlashPartial = $" does not start with a '/'";
        public const string GlobCannotEndWithSingleSlashTwoAsterisksPartial = $" ends with an unsupported sequence '{GlobConstants.SingleSlashTwoAsterisks}' and will not match. Replace it with '{GlobConstants.SingleSlash}'";
        public const string GlobCannotEndWithSingleSlashTwoAsterisksSingleSlashPartial = $" ends with an unsupported sequence '{GlobConstants.SingleSlashTwoAsterisksSingleSlash}' and will not match. Replace it with '{GlobConstants.SingleSlash}'";
        public const string GlobCannotEndInWildCardPartial = $" ends in a wildcard '{GlobConstants.SingleAsterisk}'. For directories use '{GlobConstants.SingleAsterisk}{GlobConstants.SingleSlash}' to match files in multiple directories starting with. For files match the file extension, '{GlobConstants.SingleAsterisk}.md' or all files in a single directory '{GlobConstants.SingleSlash}{GlobConstants.SingleAsterisk}'";

        // Invalid CODEOWNERS Glob Character Error messages
        public const string ContainsEscapedPoundPartial = $" contains {GlobConstants.EscapedPound}. Escaping a pattern starting with # using \\ so it is treated as a pattern and not a comment will not work in CODEOWNERS";
        public const string ContainsNegationPartial = $" contains {GlobConstants.ExclamationMark}. Using {GlobConstants.ExclamationMark} to negate a pattern will not work in CODEOWNERS";
        public const string ContainsQuestionMarkPartial = $" contains {GlobConstants.QuestionMark}. Please use {GlobConstants.SingleAsterisk} instead.";
        public const string ContainsRangePartial = $" contains {GlobConstants.LeftBracket} and/or {GlobConstants.RightBracket}. Character ranges will not work in CODEOWNERS";

        // Invalid CODEOWNERS Glob Patterns Error messages
        public const string PathIsSingleSlash = $"Path is '{GlobConstants.SingleSlash}' and will never match anything. Use '{GlobConstants.SingleSlashTwoAsterisks}' instead.";
        public const string PathIsSingleSlashTwoAsterisksSingleSlash = $"Path is '{GlobConstants.SingleSlashTwoAsterisksSingleSlash}' which is invalid. Use '{GlobConstants.SingleSlashTwoAsterisks}' instead.";

        // Codeowners glob is syntactically but doesn't match anything
        public const string GlobHasNoMatchesInRepoPartial = " glob does not have any matches in repository.";

        // Block formatting errors. These errors are specifically around validation blocks. For example, the AzureSdkOwner moniker needs
        // to part of a block that ends in a source path/owners line so it's known what they own.
        public const string ServiceLabelNeedsOwners = $"{MonikerConstants.ServiceLabel} needs to be followed by, {MonikerConstants.MissingFolder} or {MonikerConstants.ServiceOwners} with owners, or a source path/owner line.";
        public const string ServiceLabelHasTooManyOwners = $"{MonikerConstants.ServiceLabel} cannot be part of a block with, {MonikerConstants.MissingFolder} or {MonikerConstants.ServiceOwners}, and a source path/owner line.";
        public const string ServiceLabelHasTooManyOwnerMonikers = $"{MonikerConstants.ServiceLabel} cannot be part of a block with both {MonikerConstants.ServiceOwners} and {MonikerConstants.MissingFolder}.";
        public const string MissingServiceLabelPartial = $" needs to be part of a block with a {MonikerConstants.ServiceLabel} entry.";
        public const string NeedsToEndWithSourceOwnerPartial = " needs to be part of a block that ends in a source path/owner line.";
        // Duplicate Moniker error
        public const string DuplicateMonikerInBlockPartial = " already exists in the block. A moniker cannot exist more than once in a block.";
        public const string AzureSdkOwnersMustBeWithServiceLabel = $"{MonikerConstants.AzureSdkOwners} must be part of a block that contains a {MonikerConstants.ServiceLabel} entry.";
        public const string ServiceOwnersMustBeWithServiceLabel = $"{MonikerConstants.ServiceOwners} must be part of a block that contains a {MonikerConstants.ServiceLabel} entry.";
    }
}
