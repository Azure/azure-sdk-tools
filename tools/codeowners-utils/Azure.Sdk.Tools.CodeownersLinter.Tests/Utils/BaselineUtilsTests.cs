using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Errors;
using Azure.Sdk.Tools.CodeownersUtils.Tests.Mocks;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.CodeownersUtils.Verification;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    public class BaselineUtilsTests
    {
        private OwnerDataUtils _ownerDataUtils;
        private RepoLabelDataUtils _repoLabelDataUtils;
        private DirectoryUtilsMock _directoryUtilsMock;
        private List<string> _tempFiles = new List<string>();

        [OneTimeSetUp]
        // Initialize a DirectoryUtilsMock, OwnerDataUtils and RepoLabelDataUtils
        public void InitTestData()
        {
            _directoryUtilsMock = new DirectoryUtilsMock();
            _ownerDataUtils = TestHelpers.SetupOwnerData();
            _repoLabelDataUtils = TestHelpers.SetupRepoLabelData();
            // None of the tests will work if the repo/label data doesn't exist.
            // While the previous function call created the test repo/label data,
            // this call just ensures no changes were made to the util that'll
            // mess things up.
            if (!_repoLabelDataUtils.RepoLabelDataExists())
            {
                throw new ArgumentException($"Test repo/label data should have been created for {TestHelpers.TestRepositoryName} but was not.");
            }
        }

        [OneTimeTearDown]
        public void CleanupTestData()
        {
            int maxTries = 5;
            // Cleanup any temporary files created by the tests
            if (_tempFiles.Count > 0)
            {
                foreach(string tempFile  in _tempFiles)
                {
                    int tryNumber = 0;
                    while (tryNumber < maxTries)
                    {
                        try
                        {
                            File.Delete(tempFile);
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception trying to delete {tempFile}. Ex={ex}");
                            // sleep 100ms and try again
                            System.Threading.Thread.Sleep(100);
                        }
                        tryNumber++;
                    }
                    // Report that the test wasn't able to delete the file after successive tries
                    if (tryNumber == maxTries)
                    {
                        Console.WriteLine($"Unable to delete {tempFile} after {maxTries}, see above for exception details.");
                    }
                }
            }
        }

        /// <summary>
        /// Test the baseline generation. Only line errors should be in the baseline, not block errors.
        /// </summary>
        /// <param name="testCodeownerFile">The CODEOWNERS file to generate the baseline for.</param>
        /// <param name="expectedBaselineFile">The baseline file that is known to be correct for the CODEOWNERS file.</param>
        [TestCase("CodeownersTestFiles/Baseline/WithBlockErrors", "CodeownersTestFiles/Baseline/WithBlockErrors_FullBaseline.txt", 4)]
        [TestCase("CodeownersTestFiles/Baseline/NoErrors", "CodeownersTestFiles/Baseline/NoErrors_EmptyBaseline.txt", 0)]
        public void TestBaselineGenerationAndFiltering(string testCodeownerFile,
                                                       string expectedBaselineFile,
                                                       int expectedNumberOfBlockErrors)
        {
            List<BaseError> actualErrors = CodeownersLinter.LintCodeownersFile(_directoryUtilsMock,
                                                                               _ownerDataUtils,
                                                                               _repoLabelDataUtils,
                                                                               testCodeownerFile);

            // Load the expected baseline file and, for sanity's sake, ensure that all errors are filtered
            BaselineUtils expectedBaselineUtils = new BaselineUtils(expectedBaselineFile);
            var filteredWithExpected = expectedBaselineUtils.FilterErrorsUsingBaseline(actualErrors);
            
            // This piece is for sanity, to verify that the the filter only filters out SingleLineErrors and leaves
            // the BlockFormattingErrors.
            if (expectedNumberOfBlockErrors > 0)
            {
                if (filteredWithExpected.Count != expectedNumberOfBlockErrors)
                {
                    string errorString = TestHelpers.FormatErrorMessageFromErrorList(filteredWithExpected);
                    Assert.Fail($"The expected baseline file {expectedBaselineFile} for test CODEOWNERS file {testCodeownerFile} should have had {expectedNumberOfBlockErrors} block errors but had {filteredWithExpected.Count}. Unexpected baseline errors\n{errorString}");
                }
            }
            else
            {
                if (filteredWithExpected.Count > 0)
                {
                    string errorString = TestHelpers.FormatErrorMessageFromErrorList(filteredWithExpected);
                    Assert.Fail($"The expected baseline file {expectedBaselineFile} for test CODEOWNERS file {testCodeownerFile} should have filtered out all the errors but filtering return {filteredWithExpected.Count} errors. Unexpected baseline errors\n{errorString}");
                }
            }

            // Since the filtering has been verified above, regenerate the filter file and ensure that the generation produces
            // the same results. AKA only the SingleLineErrors are filtered out.
            string baselineTemp = GenerateTempFile();
            BaselineUtils generatedBaselineUtils = new BaselineUtils(baselineTemp);
            generatedBaselineUtils.GenerateBaseline(actualErrors);
            var filteredWithGenerated = generatedBaselineUtils.FilterErrorsUsingBaseline(actualErrors);

            if (expectedNumberOfBlockErrors > 0)
            {
                if (filteredWithGenerated.Count != expectedNumberOfBlockErrors)
                {
                    string errorString = TestHelpers.FormatErrorMessageFromErrorList(filteredWithGenerated);
                    Assert.Fail($"The generated baseline file {expectedBaselineFile} for test CODEOWNERS file {testCodeownerFile} should have had {expectedNumberOfBlockErrors} block errors but had {filteredWithGenerated.Count}. Unexpected baseline errors\n{errorString}");
                }
            }
            else
            {
                if (filteredWithGenerated.Count > 0)
                {
                    string errorString = TestHelpers.FormatErrorMessageFromErrorList(filteredWithGenerated);
                    Assert.Fail($"The generated baseline file {expectedBaselineFile} for test CODEOWNERS file {testCodeownerFile} should have filtered out all the errors but filtering return {filteredWithGenerated.Count} errors. Unexpected baseline errors\n{errorString}");
                }
            }
        }

        /// <summary>
        /// Generate a temporary file to be used for testing and save the filename
        /// to be cleaned up in a function called in a OneTimeTearDown
        /// </summary>
        /// <returns>string which is the full path to the temp file</returns>
        private string GenerateTempFile()
        {
            string tempFile = Path.GetTempFileName();
            _tempFiles.Add(tempFile);
            return tempFile;
        }
    }
}
