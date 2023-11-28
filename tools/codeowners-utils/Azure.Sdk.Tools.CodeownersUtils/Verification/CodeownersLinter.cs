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
    /// The primary entry point for Linting a CODEOWNERS file
    /// </summary>
    public static class CodeownersLinter
    {
        /// <summary>
        /// Load the Codeowners file and process it a block at a time
        /// </summary>
        /// <param name="directoryUtils">Directory utils used for source path entry verification.</param>
        /// <param name="ownerData">Owner data used for owner verification.</param>
        /// <param name="repoLabelData">Repository label data used for label verification.</param>
        /// <param name="codeownersFileFullPath">Codeowners file with full path</param>
        /// <returns></returns>
        public static List<BaseError> LintCodeownersFile(DirectoryUtils directoryUtils,
                                                         OwnerDataUtils ownerData,
                                                         RepoLabelDataUtils repoLabelData,
                                                         string codeownersFileFullPath)
        {
            List<BaseError> errors = new List<BaseError>();
            // Load the codeowners file and process it a block at a time 
            List<string> codeownersFile = FileHelpers.LoadFileAsStringList(codeownersFileFullPath);

            // Start parsing the codeowners file, a block at a time.
            // A block can be one of the following:
            // 1. A single source path/owner line
            // 2. One or more monikers that either ends in source path/owner line or a blank line, depending
            //    on the moniker.
            for (int currentLineNum = 0; currentLineNum < codeownersFile.Count; currentLineNum++)
            {
                string line = codeownersFile[currentLineNum];
                if (ParsingUtils.IsMonikerOrSourceLine(line))
                {
                    // A block can be a single line, if it's a source path/owners line, or if the block starts
                    // with a moniker, it'll be multi-line
                    int blockEnd = ParsingUtils.FindBlockEnd(currentLineNum, codeownersFile);
                    VerifyBlock(directoryUtils,
                                ownerData,
                                repoLabelData,
                                errors,
                                currentLineNum,
                                blockEnd,
                                codeownersFile);
                    // After processing the block, set the current line to the end line which will get
                    // incremented and continue the processing the line after the block
                    currentLineNum = blockEnd;
                }
            }
            return errors;
        }

        /// <summary>
        /// Basically a call to VerifyBlock that'll skip single line verification. This is used by the parser
        /// which only needs to know if there are block errors. Because there's no single line verification
        /// the DirectoryUtils, OwnerDataUtils and RepoLabelDataUtils are unnecessary and can be null.
        /// </summary>
        /// <param name="errors">List of errors that will be appended to if any are found with the block</param>
        /// <param name="startBlockLineNumber">The line number which is the start of the block to verify</param>
        /// <param name="endBlockLineNumber">Int, the line number that marks the of the block</param>
        /// <param name="codeownersFile">The List&lt;string&gt; that represents the CODEOWNERS file</param>
        public static void VerifyBlock(List<BaseError> errors,
                                       int startBlockLineNumber,
                                       int endBlockLineNumber,
                                       List<string> codeownersFile)
        {
            VerifyBlock(null,
                        null,
                        null,
                        errors,
                        startBlockLineNumber,
                        endBlockLineNumber,
                        codeownersFile,
                        false /* don't do single line verification */ );
        }

        /// <summary>
        /// Verify the formatting of a block in codeowners.
        /// Definitions:
        ///     Source path/Owner Line: Any line in CODEOWNERS that is not a comment and not blank.
        ///     Metadata block : A metadata block is a block that consists of one or more metadata tags which, depending on the tags,
        ///                      may end with a source path/owner line.
        /// </summary>
        /// <param name="directoryUtils">Owner data used for owner verification.</param>
        /// <param name="ownerData">Owner data used for owner verification.</param>
        /// <param name="repoLabelData">Repository label data used for label verification.</param>
        /// <param name="errors">List of errors that will be appended to if any are found with the block</param>
        /// <param name="startBlockLineNumber">The line number which is the start of the block to verify</param>
        /// <param name="endBlockLineNumber">Int, the line number that marks the of the block</param>
        /// <param name="codeownersFile">The List&lt;string&gt; that represents the CODEOWNERS file</param>
        /// <param name="singleLineVerification">Whether or not to perform single line verification, default is true. The 
        /// reason this would be turned off would be parsing, which just needs to verify the block is good.</param>
        public static void VerifyBlock(DirectoryUtils directoryUtils,
                                       OwnerDataUtils ownerData,
                                       RepoLabelDataUtils repoLabelData,
                                       List<BaseError> errors,
                                       int startBlockLineNumber, 
                                       int endBlockLineNumber, 
                                       List<string> codeownersFile,
                                       bool singleLineVerification = true)
        {
            List<string> blockErrorStrings = new List<string>();
            // The codeownersFile as a list<string> is 0 based, for reporting purposes it needs
            // to be 1 based to match the exact line in the CODEOWNERS file.
            int startLineNumberForReporting = startBlockLineNumber + 1;
            int endLineNumberForReporting = endBlockLineNumber + 1;
            bool endsWithSourceOwnerLine = ParsingUtils.IsSourcePathOwnerLine(codeownersFile[endBlockLineNumber]);
            // Booleans for every moniker, will be set to true when found, are used to verify the block
            // contains what it needs to contain for the monikers found within it.
            bool blockHasAzureSdkOwners = false;
            bool blockHasMissingFolder = false;
            bool blockHasPRLabel = false;
            bool blockHasServiceLabel = false;
            bool blockHasServiceOwners = false;

            for (int blockLine = startBlockLineNumber; blockLine <= endBlockLineNumber; blockLine++)
            {
                string line = codeownersFile[blockLine];
                int lineNumberForReporting = blockLine + 1;
                bool isSourcePathOwnerLine = ParsingUtils.IsSourcePathOwnerLine(line);
                if (isSourcePathOwnerLine)
                {
                    if (singleLineVerification)
                    {
                        VerifySingleLine(directoryUtils,
                                         ownerData,
                                         repoLabelData,
                                         errors,
                                         lineNumberForReporting,
                                         line,
                                         isSourcePathOwnerLine,
                                         !endsWithSourceOwnerLine);
                    }
                }
                else
                {
                    string moniker = MonikerUtils.ParseMonikerFromLine(line);
                    // This can happen if there's a comment line in the block, skip the line
                    if (null == moniker)
                    {
                        continue;
                    }
                    switch (moniker)
                    {
                        case MonikerConstants.AzureSdkOwners:
                            if (blockHasAzureSdkOwners)
                            {
                                blockErrorStrings.Add($"{MonikerConstants.AzureSdkOwners}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}");
                            }
                            blockHasAzureSdkOwners = true;
                            break;
                        case MonikerConstants.PRLabel:
                            if (blockHasPRLabel)
                            {
                                blockErrorStrings.Add($"{MonikerConstants.PRLabel}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}");
                            }
                            blockHasPRLabel = true;
                            break;
                        case MonikerConstants.MissingFolder:
                            if (blockHasMissingFolder)
                            {
                                blockErrorStrings.Add($"{MonikerConstants.MissingFolder}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}");
                            }
                            blockHasMissingFolder = true;
                            break;
                        case MonikerConstants.ServiceLabel:
                            if (blockHasServiceLabel)
                            {
                                blockErrorStrings.Add($"{MonikerConstants.ServiceLabel}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}");
                            }
                            blockHasServiceLabel = true;
                            break;
                        case MonikerConstants.ServiceOwners:
                            if (blockHasServiceOwners)
                            {
                                blockErrorStrings.Add($"{MonikerConstants.ServiceOwners}{ErrorMessageConstants.DuplicateMonikerInBlockPartial}");
                            }
                            blockHasServiceOwners = true;
                            break;
                        default:
                            // This shouldn't get here unless someone adds a new moniker and forgets to add it to the switch statement
                            throw new ArgumentException($"Unexpected moniker '{moniker}' found  on line {lineNumberForReporting}\nLine={line}");
                    }

                    if (singleLineVerification)
                    {
                        VerifySingleLine(directoryUtils,
                                         ownerData,
                                         repoLabelData,
                                         errors,
                                         lineNumberForReporting,
                                         line,
                                         isSourcePathOwnerLine,
                                         !endsWithSourceOwnerLine, // If the block ends in a source path/owner line then we don't expect owners on moniker lines
                                         moniker);
                    }
                }
            }

            // After the block has been processed, ensure that any monikers are paired correctly with other
            // monikers or source path/owners

            // If the block is a single source path/owners line then there's nothing else to be done since there
            // can't be any block errors.
            if (startBlockLineNumber == endBlockLineNumber && endsWithSourceOwnerLine)
            {
                return;
            }

            // AzureSdkOwners must be part of a block of that a ServiceLabel entry as the AzureSdkOwners are associated with
            // that ServiceLabel
            if (blockHasAzureSdkOwners && !blockHasServiceLabel)
            {
                blockErrorStrings.Add(ErrorMessageConstants.AzureSdkOwnersMustBeWithServiceLabel);
            }

            if (blockHasServiceOwners && !blockHasServiceLabel)
            {
                blockErrorStrings.Add(ErrorMessageConstants.ServiceOwnersMustBeWithServiceLabel);
            }

            // PRLabel moniker must be in a block that ends with a source path/owner line
            if (blockHasPRLabel && !endsWithSourceOwnerLine)
            {
                blockErrorStrings.Add($"{MonikerConstants.PRLabel}{ErrorMessageConstants.NeedsToEndWithSourceOwnerPartial}");
            }

            // ServiceLabel needs to be part of a block that has one of, ServiceOwners or #/<NotInRepo>/ (MonikerConstants.MissingFolder),
            // or ends in a source path/owner line both not both.
            if (blockHasServiceLabel)
            {
                if (!endsWithSourceOwnerLine && !blockHasServiceOwners && !blockHasMissingFolder)
                {
                    blockErrorStrings.Add(ErrorMessageConstants.ServiceLabelNeedsOwners);
                }
                else if (endsWithSourceOwnerLine && (blockHasServiceOwners || blockHasMissingFolder))
                {
                    blockErrorStrings.Add(ErrorMessageConstants.ServiceLabelHasTooManyOwners);
                }
                else if (blockHasServiceOwners && blockHasMissingFolder)
                {
                    blockErrorStrings.Add(ErrorMessageConstants.ServiceLabelHasTooManyOwnerMonikers);
                }
            }

            if (blockErrorStrings.Count > 0)
            {
                List<string> blockLines = new List<string>();
                blockLines.AddRange(codeownersFile.GetRange(startBlockLineNumber, (endBlockLineNumber - startBlockLineNumber) + 1));
                errors.Add(new BlockFormattingError(startLineNumberForReporting,
                                                    endLineNumberForReporting,
                                                    blockLines,
                                                    blockErrorStrings));

            }
        }

        /// <summary>
        /// Verify the contents of a single line, called as part of the block processing.
        /// </summary>
        /// <param name="ownerData">Owner data used for owner verification.</param>
        /// <param name="repoLabelData">Repository label data used for label verification.</param>
        /// <param name="errors">List of errors that will be appended to if any are found with the block</param>
        /// <param name="lineNumberForReporting">The line number, for reporting purposes, of the line being processed.</param>
        /// <param name="line">The CODEOWNERS line to process.</param>
        /// <param name="expectOwnersIfMoniker">True if owners are expected on a moniker line. This would be true if the moniker is part of a block that didn't end in a source path/owner line.</param>
        public static void VerifySingleLine(DirectoryUtils directoryUtils,
                                            OwnerDataUtils ownerData,
                                            RepoLabelDataUtils repoLabelData,
                                            List<BaseError> errors, 
                                            int lineNumberForReporting, 
                                            string line,
                                            bool isSourcePathOwnerLine,
                                            bool expectOwnersIfMoniker,
                                            string moniker = null)
        {
            List<string> errorStrings = new List<string>();
            if (isSourcePathOwnerLine)
            {
                // Verify the source path and owners
                directoryUtils.VerifySourcePathEntry(line, errorStrings);
                Owners.VerifyOwners(ownerData, line, true, errorStrings);
            }
            else
            {
                // At this point, the moniker shouldn't be null, comment lines should have been
                // sifted out by the calling method.
                if (null != moniker)
                {
                    switch (moniker)
                    {
                        // Both ServiceLabel and blockHasPRLabel moniker lines need to have labels
                        case MonikerConstants.ServiceLabel:
                        case MonikerConstants.PRLabel:
                            Labels.VerifyLabels(repoLabelData, line, moniker, errorStrings);
                            break;
                        // MissingFolder, ServiceOwners and AzureSdkOwners
                        case MonikerConstants.MissingFolder:
                        case MonikerConstants.ServiceOwners:
                        case MonikerConstants.AzureSdkOwners:
                            Owners.VerifyOwners(ownerData, line, expectOwnersIfMoniker, errorStrings);
                            break;
                        default:
                            // This shouldn't get here unless someone adds a new moniker and forgets to add it to the switch statement
                            throw new ArgumentException($"Unexpected moniker '{moniker}' found  on line {lineNumberForReporting}\nLine={line}");
                    }
                }
            }
            // If any errors were encountered on the line, create a new exception and add it to the list
            if (errorStrings.Count > 0)
            {
                errors.Add(new SingleLineError(lineNumberForReporting, line, errorStrings));
            }
        }
    }
}
