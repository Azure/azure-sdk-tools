using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Tests.Mocks;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Parsing
{
    public class CodeownersParserTests
    {
        private OwnerDataUtils _ownerDataUtils;
        private RepoLabelDataUtils _repoLabelDataUtils;
        private DirectoryUtilsMock _directoryUtilsMock;
        private CodeownersEntry _emptyEntry;

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
            _emptyEntry = new CodeownersEntry();
        }

        /// <summary>
        /// Test ParseCodeownersFile. Loads a test codeowners file and compares that against the expected output
        /// deserialized from Json. The thing that's different about parsing vs verification is that any individual
        /// lines, themselves, are not validted. The only thing that is validated is that the block doesn't have any
        /// errors. Any blocks with errors are ignored and won't be in the returned list of Codeowners entries.
        /// </summary>
        /// <param name="codeownersFile">The test CODEOWNERS file to parse</param>
        /// <param name="jsonFileWithExpectedEntries">The json file with the expected Codeowners entries.</param>
        [TestCase("CodeownersTestFiles/EndToEnd/NoErrors", "CodeownersTestFiles/EndToEnd/NoErrorsExpectedEntries.json")]
        // This is to ensure that block entries with errors are not parsed
        [TestCase("CodeownersTestFiles/EndToEnd/WithBlockErrors", "CodeownersTestFiles/EndToEnd/WithBlockErrorsExpectedEntries.json")]
        public void TestParseCodeownersFile(string codeownersFile, string jsonFileWithExpectedEntries)
        {
            List<CodeownersEntry> actualEntries = CodeownersParser.ParseCodeownersFile(codeownersFile);

            string expectedEntriesJson = FileHelpers.GetFileOrUrlContents(jsonFileWithExpectedEntries);
            List<CodeownersEntry> expectedEntries = JsonSerializer.Deserialize<List<CodeownersEntry>>(expectedEntriesJson);
            bool entiresAreEqual = TestHelpers.CodeownersEntryListsAreEqual(actualEntries, expectedEntries);
            if (!entiresAreEqual)
            {
                Assert.Fail(TestHelpers.FormatCodeownersListDifferences(actualEntries, expectedEntries));
            }
        }

        /// <summary>
        /// Test the GetMatchingCodeownersEntry. Ultimately, this uses the DictoryUtils.PathExpressionMatchesTargetPath
        /// to match the targetPath against the CodeownersEntry's PathExpression in reverse order and it's this piece
        /// that's being tested.
        /// </summary>
        /// <param name="codeownersFile">The test CODEOWNERS file to parse</param>
        /// <param name="targetPath">The target path to find a match for</param>
        /// <param name="expectMatch">Whether or not a match is expected</param>
        /// <param name="expectedMatchPathExpression">If the match is expected, the PathExpression that is expected to match</param>
        // Targer path doesn't exist in the repository CODEOWNERS, should hit the global fallback
        [TestCase("CodeownersTestFiles/Matching/SubDirAndFallback", "sdk/NotInCODEOWNERSFILE/foo.yml", true, "/**")]
        // Default tests, these should straight up match the sdk/ServiceDirectory* entries
        [TestCase("CodeownersTestFiles/Matching/SubDirAndFallback", "sdk/ServiceDirectory1/SomeSubDirectory1/SomeFile1.md", true, "/sdk/ServiceDirectory1/")]
        [TestCase("CodeownersTestFiles/Matching/SubDirAndFallback", "sdk/ServiceDirectory2/SomeSubDirectory2/SomeFile2.md", true, "/sdk/ServiceDirectory2/")]
        [TestCase("CodeownersTestFiles/Matching/SubDirAndFallback", "sdk/ServiceDirectory2/azure-service1-test/SomeFile3.md", true, "/sdk/**/azure-service1-*/")]
        [TestCase("CodeownersTestFiles/Matching/SubDirAndFallback", "sdk/ServiceDirectory4/azure-service1-test2/SomeFile4.md", true, "/sdk/**/azure-service1-*/")]
        // Entry in logs directory, regardless of where it is in the repository
        [TestCase("CodeownersTestFiles/Matching/SubDirAndFallback", "sdk/ServiceDirectory1/azure-service1-test5/logs/LogFile.txt", true, "/**/logs/*")]
        // Similar but not quite
        [TestCase("CodeownersTestFiles/Matching/SubDirAndFallback", "sdk/ServiceDirectory1/azure-service1-test5/Notlogs/NotLogFile.txt", true, "/sdk/**/azure-service1-*/")]
        public void TestGetMatchingCodeownersEntry(string codeownersFile, string targetPath, bool expectMatch, string expectedMatchPathExpression)
        {
            List<CodeownersEntry> parsedEntries = CodeownersParser.ParseCodeownersFile(codeownersFile);
            CodeownersEntry matchedEntry = CodeownersParser.GetMatchingCodeownersEntry(targetPath, parsedEntries);
            if (!expectMatch)
            {
                Assert.That(matchedEntry, Is.EqualTo(_emptyEntry), $"No match was expected but the following entry was returned.\n{matchedEntry}");
            }
            else
            {
                Assert.That(matchedEntry.PathExpression, Is.EqualTo(expectedMatchPathExpression), $"Expected matching entry for targetPath, {targetPath}, to match {expectedMatchPathExpression} but {matchedEntry.PathExpression} was returned instead.");
            }
        }
    }
}
