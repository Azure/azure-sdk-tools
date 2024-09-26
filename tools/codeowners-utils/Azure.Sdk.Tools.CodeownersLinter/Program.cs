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
            var teamUserBlobStorageUriOption = new Option<string>
                (name: "--teamUserBlobStorageURI",
                description: "The team/user blob storage URI.");
            teamUserBlobStorageUriOption.AddAlias("-tUri");
            teamUserBlobStorageUriOption.IsRequired = true;

            var userOrgVisibilityBlobStorageUriOption = new Option<string>
                (name: "--userOrgVisibilityBlobStorageURI",
                description: "The user/org blob storage URI.");
            userOrgVisibilityBlobStorageUriOption.AddAlias("-uUri");
            userOrgVisibilityBlobStorageUriOption.IsRequired = true;

            var repoLabelBlobStorageUriOption = new Option<string>
                (name: "--repoLabelBlobStorageURI",
                description: "The repo/label blob storage URI.");
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

            var baseBranchBaselineFileOption = new Option<string>
                (name: "--baseBranchBaselineFile",
                description: "The full path to base branch baseline file to be generated or used. The file will be generated if -gbl is set and used to further filter errors if -fbl is set.");
            baseBranchBaselineFileOption.AddAlias("-bbf");
            baseBranchBaselineFileOption.IsRequired = false;
            baseBranchBaselineFileOption.SetDefaultValue(null);

            var rootCommand = new RootCommand
            {
                teamUserBlobStorageUriOption,
                userOrgVisibilityBlobStorageUriOption,
                repoLabelBlobStorageUriOption,
                repoRootOption,
                repoNameOption,
                filterBaselineErrorsOption,
                generateBaselineOption,
                baseBranchBaselineFileOption
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
                    string baseBranchBaselineFile = context.ParseResult.GetValueForOption(baseBranchBaselineFileOption);
                    returnCode = LintCodeownersFile(teamUserBlobStorageUri,
                                                    userOrgVisibilityBlobStorageUri,
                                                    repoLabelBlobStorageUri,
                                                    repoRoot,
                                                    repoName,
                                                    filterBaselineErrors,
                                                    generateBaseline,
                                                    baseBranchBaselineFile);
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
        /// 
        /// The baseBranchBaselineFile
        /// This file will be primarily used in PR validation where two calls will be made. be made. The first
        /// call will use the -gbl option and generate the secondary file to a different location. It can't use
        /// the standard CODEOWNERS_baseline_error.txt file because it'll be being used in combination with this
        /// file. The second call, to verify CODEOWNERS changes in the PR, will verify against the default
        /// CODEOWNERS_baseline_error.txt and, if there are any remaining errors, check to see if those exist in the
        /// secondary baseline file. The reason for doing this is prevent PRs from being blocked if there are issues
        /// in the baseline branch. The typical scenario here will be people leaving the company, the base branch's
        /// CODEOWNERS hasn't yet been updated to reflect this and because of that any PRs with CODEOWNERS changes
        /// would get blocked. If the remaining errors in the PR validation exist in the base branch's errors then
        /// the linter will return a pass instead of a failure.
        /// </summary>
        /// <param name="teamUserBlobStorageUri">URI of the team/user data in blob storate</param>
        /// <param name="userOrgVisibilityBlobStorageUri">URI of the org visibility in blob storage</param>
        /// <param name="repoLabelBlobStorageUri">URI of the repository label data</param>
        /// <param name="repoRoot">The root of the repository</param>
        /// <param name="repoName">The repository name, including org. Eg. Azure/azure-sdk</param>
        /// <param name="filterBaselineErrors">Boolean, if true then errors should be filtered using the repository's baseline.</param>
        /// <param name="generateBaseline">Boolean, if true then regenerate the baseline file from the error encountered during parsing.</param>
        /// <param name="baseBranchBaselineFile">The name of the base branch baseline file to generate or use.</param>
        /// <returns>integer, used to set the return code</returns>
        /// <exception cref="ArgumentException">Thrown if any arguments, or argument combinations, are invalid.</exception>
        static int LintCodeownersFile(string teamUserBlobStorageUri, 
                                      string userOrgVisibilityBlobStorageUri, 
                                      string repoLabelBlobStorageUri, 
                                      string repoRoot, 
                                      string repoName,
                                      bool   filterBaselineErrors,
                                      bool   generateBaseline,
                                      string baseBranchBaselineFile)
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

            bool useBaseBranchBaselineFile = false;
            if (!string.IsNullOrEmpty(baseBranchBaselineFile))
            {
                if ((filterBaselineErrors && File.Exists(baseBranchBaselineFile)) || generateBaseline)
                {
                    useBaseBranchBaselineFile = true;
                }
                else
                {
                    throw new ArgumentException($"The base branch baseline file {baseBranchBaselineFile} does not exist.");
                }
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
                BaselineUtils baselineUtils = null;
                if (useBaseBranchBaselineFile)
                {
                    baselineUtils = new BaselineUtils(baseBranchBaselineFile);
                }
                else
                {
                    baselineUtils = new BaselineUtils(codeownersBaselineFile);
                }
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

                // After the file has been filered with the standard CODEOWNERS baseline file, if there are
                // still remaining errors and there is a base branch baseline file, further filter with that
                // file.
                if (useBaseBranchBaselineFile && errors.Count > 0)
                {
                    BaselineUtils baselineUtils = new BaselineUtils(baseBranchBaselineFile);
                    errors = baselineUtils.FilterErrorsUsingBaseline(errors);
                }
            }
            bool loggingInDevOps = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID"));
            int returnCode = 0;
            // If there are errors, and this isn't a baseline generation, ensure the returnCode is non-zero and output the errors.
            if ((errors.Count > 0) && !generateBaseline)
            {
                returnCode = 1;

                // DevOps only adds the first 4 errors to the github checks list so lets always add the generic one first and then as many of the individual ones as can be found afterwards
                if (loggingInDevOps)
                {
                    Console.WriteLine($"##vso[task.logissue type=error;]There are linter errors. Please visit {linterErrorsHelpLink} for guidance on how to handle them.");
                }
                else
                {
                    Console.WriteLine($"There are linter errors. Please visit {linterErrorsHelpLink} for guidance on how to handle them.");
                }

                // Output the errors sorted ascending by line number and by type. If there's a block
                // error with the same starting line number as a single line error, the block error
                // should be output first.
                var errorsByLineAndType = errors.OrderBy(e => e.LineNumber).ThenBy(e => e.GetType().Name);

                foreach (var error in errorsByLineAndType)
                {
                    if (loggingInDevOps)
                    {
                        // Environment.NewLine needs to be replaced by an encoded NewLine "%0D%0A" in order to display on GitHub and DevOps checks
                        Console.WriteLine($"##vso[task.logissue type=error;sourcepath={codeownersFileFullPath};linenumber={error.LineNumber};columnnumber=1;]{error.ToString().Replace(Environment.NewLine,"%0D%0A")}");
                    }
                    else 
                    {
                        Console.WriteLine(error + Environment.NewLine);
                    }
                }
            }
            return returnCode;
        }
    }
}
