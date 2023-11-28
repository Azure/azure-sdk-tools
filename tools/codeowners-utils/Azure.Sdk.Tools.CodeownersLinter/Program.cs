using System.CommandLine;
using System.Diagnostics;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Errors;
using Azure.Sdk.Tools.CodeownersUtils.Verification;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Sdk.Tools.CodeownersLinter
{
    internal class Program
    {
        const string linterErrorsHelpLink = "https://aka.ms/azsdk/codeownersLinterErrors";
        static void Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // The storage URIs are in the azure-sdk-write-teams-container-blobs pipeline variable group.
            // The URIs do not contain the SAS.
            var teamUserBlobStorageUriOption = new Option<string>
                (name: "--teamUserBlobStorageURI",
                description: "The team/user blob storage URI without the SAS.");
            teamUserBlobStorageUriOption.AddAlias("-tUri");
            teamUserBlobStorageUriOption.IsRequired = true;

            var userOrgVisibilityBlobStorageUriOption = new Option<string>
                (name: "--userOrgVisibilityBlobStorageURI",
                description: "The user/org blob storage URI without the SAS.");
            userOrgVisibilityBlobStorageUriOption.AddAlias("-uUri");
            userOrgVisibilityBlobStorageUriOption.IsRequired = true;

            var repoLabelBlobStorageUriOption = new Option<string>
                (name: "--repoLabelBlobStorageURI",
                description: "The repo/label blob storage URI without the SAS.");
            repoLabelBlobStorageUriOption.AddAlias("-rUri");
            repoLabelBlobStorageUriOption.IsRequired = true;

            // In a pipeline the repoRoot option should be as follows on the command line
            // --repoRoot $(Build.SourcesDirectory)
            var repoRootOption = new Option<string>
                (name: "--repoRoot",
                description: "The root of the repository to be scanned.");
            repoRootOption.IsRequired = true;

            // In a pipeline, the repo name option should be as follows on the command line
            // --repoName $(Build.Repository.Name)
            var repoNameOption = new Option<string>
                (name: "--repoName",
                description: "The name of the repository.");
            repoNameOption.IsRequired = true;

            // Whether or not to use the baseline. If this option is selected it'll
            // load the baseline file that sits beside the CODEOWNERS file and only
            // report errors that don't exist in the baseline.
            // Note, this is flag and flags default to false if they're not on the command line.
            var filterBaselineErrorsOption = new Option<bool>
                (name: "--filterBaselineErrors",
                description: "Only output errors that don't exist in the baseline.");
            filterBaselineErrorsOption.AddAlias("-fbl");

            var generateBaselineOption = new Option<bool>
                (name: "--generateBaseline",
                description: "Generate the baseline error file.");
            generateBaselineOption.AddAlias("-gbl");

            var rootCommand = new RootCommand
            {
                teamUserBlobStorageUriOption,
                userOrgVisibilityBlobStorageUriOption,
                repoLabelBlobStorageUriOption,
                repoRootOption,
                repoNameOption,
                filterBaselineErrorsOption,
                generateBaselineOption
            };

            int returnCode = 1;
            // This might look a bit odd. System.CommandLine cannot have a non-async handler that isn't a void
            // which means that the call only returns non-zero if the handler call fails. Instead of setting the
            // handler to the function with the option arguments, the handler needs to take the context, grab
            // all of the option values, call the function and set the local variable to the return value.
            // Q) Why is this necessary?
            // A) The linting of the CODEOWNERS file needs to be able to provide a return value for success and
            //    failure. The only way the handler returns a failure, in the non-async, case would be something
            //    like an unhandled exception.
            rootCommand.SetHandler(
                (context) =>
                {
                    string teamUserBlobStorageUri = context.ParseResult.GetValueForOption(teamUserBlobStorageUriOption);
                    string userOrgVisibilityBlobStorageUri = context.ParseResult.GetValueForOption(userOrgVisibilityBlobStorageUriOption);
                    string repoLabelBlobStorageUri = context.ParseResult.GetValueForOption(repoLabelBlobStorageUriOption);
                    string repoRoot = context.ParseResult.GetValueForOption(repoRootOption);
                    string repoName = context.ParseResult.GetValueForOption(repoNameOption);
                    bool filterBaselineErrors = context.ParseResult.GetValueForOption(filterBaselineErrorsOption);
                    bool generateBaseline = context.ParseResult.GetValueForOption(generateBaselineOption);
                    returnCode = LintCodeownersFile(teamUserBlobStorageUri,
                                                    userOrgVisibilityBlobStorageUri,
                                                    repoLabelBlobStorageUri,
                                                    repoRoot,
                                                    repoName,
                                                    filterBaselineErrors,
                                                    generateBaseline);
                });

            rootCommand.Invoke(args);

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine($"Total run time: {elapsedTime}");

            Console.WriteLine($"Exiting with return code {returnCode}");
            Environment.Exit(returnCode);
        }

        /// <summary>
        /// Verify the arguments and call to process the CODEOWNERS file. If errors are being filtered with a 
        /// baseline, or used to regenerate the baseline, that's done in here. Note that filtering errors and
        /// regenerating the baseline cannot both be done in the same run.
        /// </summary>
        /// <param name="teamUserBlobStorageUri">URI of the team/user data in blob storate</param>
        /// <param name="userOrgVisibilityBlobStorageUri">URI of the org visibility in blob storage</param>
        /// <param name="repoLabelBlobStorageUri">URI of the repository label data</param>
        /// <param name="repoRoot">The root of the repository</param>
        /// <param name="repoName">The repository name, including org. Eg. Azure/azure-sdk</param>
        /// <param name="filterBaselineErrors">Boolean, if true then errors should be filtered using the repository's baseline.</param>
        /// <param name="generateBaseline">Boolean, if true then regenerate the baseline file from the error encountered during parsing.</param>
        /// <returns>integer, used to set the return code</returns>
        /// <exception cref="ArgumentException">Thrown if any arguments, or argument combinations, are invalid.</exception>
        static int LintCodeownersFile(string teamUserBlobStorageUri, 
                                      string userOrgVisibilityBlobStorageUri, 
                                      string repoLabelBlobStorageUri, 
                                      string repoRoot, 
                                      string repoName,
                                      bool   filterBaselineErrors,
                                      bool   generateBaseline)
        {
            // Don't allow someone to create and use a baseline in the same run
            if (filterBaselineErrors && generateBaseline)
            {
                throw new ArgumentException("The --filterBaselineErrors (-fbl) and --generateBaseline (-gbl) options cannot both be set. Either a baseline is being generated or being used to filter but not both.");
            }

            // Verify that the repoRoot exists
            if (!Directory.Exists(repoRoot))
            {
                throw new ArgumentException($"The repository root '{repoRoot}' is not a valid directory. Please ensure the --repoRoot is set to the root of the repository.");
            }
            // Verify that the CODEOWNERS file exists in the .github subdirectory of the repository root
            string codeownersFileFullPath = Path.Combine(repoRoot, ".github", "CODEOWNERS");
            if (!File.Exists(codeownersFileFullPath))
            {
                throw new ArgumentException($"CODEOWNERS file {codeownersFileFullPath} does not exist. Please ensure the --repoRoot is set to the root of the repository and the CODEOWNERS file exists in the .github subdirectory.");
            }
            // Verify that label data exists for the repository
            RepoLabelDataUtils repoLabelData = new RepoLabelDataUtils(repoLabelBlobStorageUri, repoName);
            if (!repoLabelData.RepoLabelDataExists())
            {
                throw new ArgumentException($"The repository label data for {repoName} does not exist. Should this be running in this repository?");
            }
            
            string codeownersBaselineFile = Path.Combine(repoRoot, ".github", BaselineConstants.BaselineErrorFile);
            bool codeownersBaselineFileExists = false;
            // If the baseline is to be used, verify that it exists.
            if (filterBaselineErrors)
            {
                if (File.Exists(codeownersBaselineFile))
                {
                    codeownersBaselineFileExists = true;
                }
                else
                {
                    Console.WriteLine($"The CODEOWNERS baseline error file, {codeownersBaselineFile}, file for {repoName} does not exist. No filtering will be done for errors.");
                }
            }

            DirectoryUtils directoryUtils = new DirectoryUtils(repoRoot);
            OwnerDataUtils ownerData = new OwnerDataUtils(teamUserBlobStorageUri, userOrgVisibilityBlobStorageUri);

            var errors = CodeownersUtils.Verification.CodeownersLinter.LintCodeownersFile(directoryUtils, 
                                                                           ownerData, 
                                                                           repoLabelData,
                                                                           codeownersFileFullPath);

            // Regenerate the baseline file if that option was selected
            if (generateBaseline)
            {
                BaselineUtils baselineUtils = new BaselineUtils(codeownersBaselineFile);
                baselineUtils.GenerateBaseline(errors);
            }

            // If the baseline is being used to filter out known errors, set the list
            // of errors to the filtered list.
            if (filterBaselineErrors)
            {
                // Can only filter if the filter file exists, if it doesn't then there's nothing to filter
                // and all encountered errors will be output. Also, if the file doesn't exist that's reported
                // above and doesn't need to be reported here.
                if (codeownersBaselineFileExists)
                {
                    if (errors.Count == 0)
                    {
                        Console.WriteLine($"##vso[task.LogIssue type=warning;]There were no CODEOWNERS parsing errors but there is a baseline file {codeownersBaselineFile} for filtering. If the file is empty, or all errors have been fixed, then it should be deleted.");
                    }
                    else
                    {
                        BaselineUtils baselineUtils = new BaselineUtils(codeownersBaselineFile);
                        errors = baselineUtils.FilterErrorsUsingBaseline(errors);
                    }
                }
            }

            int returnCode = 0;
            // If there are errors, ensure the returnCode is non-zero and output the errors.
            if (errors.Count > 0)
            {
                returnCode = 1;

                // Output the errors sorted ascending by line number and by type. If there's a block
                // error with the same starting line number as a single line error, the block error
                // should be output first.
                var errorsByLineAndType = errors.OrderBy(e => e.LineNumber).ThenBy(e => e.GetType().Name);

                foreach (var error in errorsByLineAndType)
                {
                    Console.WriteLine(error + Environment.NewLine);
                }

                Console.WriteLine($"There were linter errors. Please visit {linterErrorsHelpLink} for guidance on how to handle them.");
            }
            return returnCode;
        }
    }
}
