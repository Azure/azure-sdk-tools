using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Errors;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.CodeownersUtils.Verification;

namespace Azure.Sdk.Tools.CodeownersUtils.Parsing
{
    /// <summary>
    /// This is the main entry point for Codeowners parsing. In the old parser this is the equivalent of CodeownersFile.
    /// This just contains the static methods for parsing the CODEOWNERS file and for retrieving matching CODEOWNS entries.
    /// </summary>
    public class CodeownersParser
    {
        /// <summary>
        /// Load the CODEOWNERS content from the file or URL and parse the entries.
        /// </summary>
        /// <param name="codeownersFilePathOrUrl"></param>
        /// <param name="teamStorageURI"></param>
        /// <returns></returns>
        public static List<CodeownersEntry> ParseCodeownersFile(string codeownersFilePathOrUrl,
                                                                string teamStorageURI = null)
        {
            List<string> codeownersFile = FileHelpers.LoadFileAsStringList(codeownersFilePathOrUrl);
            return ParseCodeownersEntries(codeownersFile, teamStorageURI);
        }

        /// <summary>
        /// Given a CODEOWNERS file as a List&gt;string&lt;, parse the Codeowners entries.
        /// <param name="codeownersFile">Codeowners file as a List&gt;string&lt;</param>
        /// <param name="teamStorageURI">The URI of the team storage data if being overridden.</param>
        /// <returns>List&gt;CodeownersEntry&lt;</returns>
        public static List<CodeownersEntry> ParseCodeownersEntries(List<string> codeownersFile,
                                                                   string teamStorageURI = null)
        {
            OwnerDataUtils ownerDataUtils = new OwnerDataUtils(teamStorageURI);
            List<CodeownersEntry> codeownersEntries = new List<CodeownersEntry>();

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
                    List<BaseError> errors = new List<BaseError>();
                    CodeownersLinter.VerifyBlock(errors,
                                                 currentLineNum,
                                                 blockEnd,
                                                 codeownersFile);
                    if (errors.Count > 0)
                    {
                        // There should only be a single block error here.
                        foreach(BaseError error in errors)
                        {
                            Console.Error.WriteLine($"Block error encountered while parsing, entry will be skipped.\n{error}");
                        }
                    }
                    else
                    {
                        codeownersEntries.Add(ParseCodeownersEntryFromBlock(ownerDataUtils, currentLineNum, blockEnd, codeownersFile));
                    }
                    // After processing the block, set the current line to the end line which will get
                    // incremented and continue the processing the line after the block
                    currentLineNum = blockEnd;
                }
            }
            return codeownersEntries;
        }

        /// <summary>
        /// Given a 
        /// </summary>
        /// <param name="ownerDataUtils">OwnerDataUtils, required for team expansion only.</param>
        /// <param name="startBlockLineNumber">Starting line number of the block.</param>
        /// <param name="endBlockLineNumber">Ending line number of the block.</param>
        /// <param name="codeownersFile">Codeowners file as a List&gt;string&lt;</param>
        /// <returns>CodeownersEntry for the parsed block.</returns>
        /// <exception cref="ArgumentException">Thrown if a moniker encountered isn't in the switch statement.</exception>
        public static CodeownersEntry ParseCodeownersEntryFromBlock(OwnerDataUtils ownerDataUtils,
                                                                    int startBlockLineNumber,
                                                                    int endBlockLineNumber,
                                                                    List<string> codeownersFile)
        {
            CodeownersEntry codeownersEntry = new CodeownersEntry();
            // If the block ends with a source path/owner line then any owner moniker line that are empty
            // will be set to the same list as the source owners.
            bool endsWithSourceOwnerLine = ParsingUtils.IsSourcePathOwnerLine(codeownersFile[endBlockLineNumber]);
            // These are used in the case where the AzureSdkOwners and/or ServiceOwners are empty and part of a
            // block that ends in a source path/owners line. This means that the either or both monikers will have
            // their owner lists set to the same owners as source owners.
            bool emptyAzureSdkOwners = false;
            bool hasServiceLabel = false;

            for (int blockLine = startBlockLineNumber; blockLine <= endBlockLineNumber; blockLine++)
            {
                string line = codeownersFile[blockLine];
                bool isSourcePathOwnerLine = ParsingUtils.IsSourcePathOwnerLine(line);
                if (isSourcePathOwnerLine)
                {
                    codeownersEntry.SourceOwners = ParsingUtils.ParseOwnersFromLine(ownerDataUtils,
                                                                                    line,
                                                                                    true /*expand teams when parsing*/);
                    // So it's clear why this is here:
                    // The original parser left the PathExpression empty if there were no source owners for a given path
                    // in order to prevent matches against a PathExpression with no source owners. The same needs to be
                    // done here for compat reasons.
                    if (codeownersEntry.SourceOwners.Count != 0)
                    {
                        codeownersEntry.PathExpression = ParsingUtils.ParseSourcePathFromLine(line);
                    }
                }
                else
                {
                    string moniker = MonikerUtils.ParseMonikerFromLine(line);
                    // A block can contain comments which is why a line in a block wouldn't have a moniker
                    if (moniker == null)
                    {
                        continue;
                    }
                    switch (moniker)
                    {
                        case MonikerConstants.AzureSdkOwners:
                            {
                                codeownersEntry.AzureSdkOwners = ParsingUtils.ParseOwnersFromLine(ownerDataUtils,
                                                                                                  line,
                                                                                                  true /*expand teams when parsing*/);
                                if (codeownersEntry.AzureSdkOwners.Count == 0)
                                {
                                    emptyAzureSdkOwners = true;
                                }
                                break;
                            }
                        case MonikerConstants.PRLabel:
                            {
                                codeownersEntry.PRLabels = ParsingUtils.ParseLabelsFromLine(line);
                                break;
                            }
                        case MonikerConstants.ServiceLabel:
                            {
                                codeownersEntry.ServiceLabels = ParsingUtils.ParseLabelsFromLine(line);
                                hasServiceLabel = true;
                                break;
                            }
                        // ServiceOwners and /<NotInRepo>/ both map to service owners.
                        case MonikerConstants.MissingFolder:
                        case MonikerConstants.ServiceOwners:
                            {
                                codeownersEntry.ServiceOwners = ParsingUtils.ParseOwnersFromLine(ownerDataUtils,
                                                                                                 line,
                                                                                                 true /*expand teams when parsing*/);
                                break;
                            }
                        default:
                            // This shouldn't get here unless someone adds a new moniker and forgets to add it to the switch statement
                            throw new ArgumentException($"Unexpected moniker '{moniker}' found  on line {blockLine+1}\nLine={line}");

                    }
                }
            }

            // Take care of the case where an empty owners moniker, in a block that ends
            // in a source path/owners moniker, uses the source owners as its owners.
            if (endsWithSourceOwnerLine)
            {
                // If the AzureSdkOwners moniker had no owners defined, the AzureSdkOwners are
                // the same as the SourceOwners
                if (emptyAzureSdkOwners)
                {
                    codeownersEntry.AzureSdkOwners = codeownersEntry.SourceOwners;
                }
                // If there was a ServiceLabel and the block ended in a source path/owners line, the 
                // ServiceOwners are the same as the SourceOWners
                if (hasServiceLabel && endsWithSourceOwnerLine)
                {
                    codeownersEntry.ServiceOwners = codeownersEntry.SourceOwners;
                }
            }
            return codeownersEntry;
        }

        /// <summary>
        /// Given a list of Codeowners entries and a target patch, return the entry that matches the target path. Note:
        /// the function processes the list in reverse order, in other words it'll return the match in the list which
        /// mirrors the way GitHub would match from the CODEOWNERS file; The last entry in the CODEOWNERS file that matches
        /// is the one that "wins".
        /// </summary>
        /// <param name="targetPath">The path to check.</param>
        /// <param name="codeownersEntries">List&gt;CodeownersEntry&lt; parsed the CODEOWNERS file in the repository that the path belongs to.</param>
        /// <returns>The CodeownersEntry that matches the target path or an empty CodeownersEntry if there is no match</returns>
        public static CodeownersEntry GetMatchingCodeownersEntry(string targetPath, List<CodeownersEntry> codeownersEntries)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(targetPath));
            CodeownersEntry matchedEntry = codeownersEntries
            .Where(entry => DirectoryUtils.PathExpressionMatchesTargetPath(entry.PathExpression, targetPath))
            // Entries listed in CODEOWNERS file below take precedence, hence we read the file from the bottom up.
            // By convention, entries in CODEOWNERS should be sorted top-down in the order of:
            // - 'RepoPath',
            // - 'ServicePath'
            // - and then 'PackagePath'.
            // However, due to lack of validation, as of 12/29/2022 this is not always the case.
            .Reverse()
            // Return the first element found or a new, empty CodeownersEntry if nothing matched
            .FirstOrDefault(new CodeownersEntry());
            return matchedEntry;
        }
    }
}
