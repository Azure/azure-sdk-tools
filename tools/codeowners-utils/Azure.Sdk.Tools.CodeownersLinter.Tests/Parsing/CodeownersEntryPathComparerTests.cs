using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using Azure.Sdk.Tools.CodeownersUtils.Tests.Mocks;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Parsing
{
    [TestFixture]
    public class CodeownersEntryPathComparerTests
    {
        private CodeownersEntryPathComparer comparer;
        private OwnerDataUtils _ownerDataUtils;
        private RepoLabelDataUtils _repoLabelDataUtils;
        private DirectoryUtilsMock _directoryUtilsMock;

        [OneTimeSetUp]
        public void InitTestData()
        {
            _directoryUtilsMock = new DirectoryUtilsMock();
            _ownerDataUtils = TestHelpers.SetupOwnerData();
            _repoLabelDataUtils = TestHelpers.SetupRepoLabelData();
            
            if (!_repoLabelDataUtils.RepoLabelDataExists())
            {
                throw new ArgumentException($"Test repo/label data should have been created for {TestHelpers.TestRepositoryName} but was not.");
            }
        }

        [SetUp]
        public void Setup()
        {
            comparer = new CodeownersEntryPathComparer();
        }

        /// <summary>
        /// Test the CodeownersEntryPathComparer with various path combinations to ensure correct sorting behavior.
        /// The comparer should sort paths in a specific order for CODEOWNERS files.
        /// </summary>
        /// <param name="inputPaths">Newline-separated string of input paths to sort</param>
        /// <param name="expectedOutput">Expected output after sorting</param>
        [TestCase("/sdk/\n/sdk/bzcore/\n/sdk/azcore/", "/sdk/\n/sdk/azcore/\n/sdk/bzcore/", 
            Description = "Parent directory should come before child directories, with child directories sorted alphabetically")]
        [TestCase("/sdk/ai/azopenai\n/sdk/ai", "/sdk/ai\n/sdk/ai/azopenai", 
            Description = "Parent directory should come before more specific child paths")]
        [TestCase("/.github/workflows/\n/.config/1espt/\n/.github/CODEOWNERS", "/.config/1espt/\n/.github/CODEOWNERS\n/.github/workflows/", 
            Description = "Multiple top-level directories should be sorted alphabetically")]
        [TestCase("/sdk/\n/eng/\n/", "/eng/\n/sdk/\n/", 
            Description = "Root path behavior with multiple top-level directories")]
        [TestCase("/eng/emitter-package.json\n/eng/emitter-package-lock.json", "/eng/emitter-package-lock.json\n/eng/emitter-package.json", 
            Description = "Files in same directory should be sorted alphabetically")]
        [TestCase("/eng/common/pipelines/codeowners-linter.yml\n/eng/common/", "/eng/common/\n/eng/common/pipelines/codeowners-linter.yml", 
            Description = "Directory should come before files within that directory")]
        [TestCase("/sdk/storage/\n/sdk/resourcemanager/\n/sdk/monitor/\n/sdk/security/keyvault/", "/sdk/monitor/\n/sdk/resourcemanager/\n/sdk/security/keyvault/\n/sdk/storage/", 
            Description = "Multiple SDK service directories should be sorted alphabetically")]
        [TestCase("/eng/tools/generator\n/eng/config.json\n/eng/", "/eng/\n/eng/config.json\n/eng/tools/generator", 
            Description = "Directory should come first, then files, then subdirectories")]
        [TestCase("/sdk/messaging/stress/\n/sdk/messaging/azwebpubsub/\n/sdk/messaging/eventgrid/\n/sdk/messaging/azservicebus/\n/sdk/messaging/azeventhubs/", "/sdk/messaging/azeventhubs/\n/sdk/messaging/azservicebus/\n/sdk/messaging/azwebpubsub/\n/sdk/messaging/eventgrid/\n/sdk/messaging/stress/", 
            Description = "Multiple subdirectories within same parent should be sorted alphabetically")]
        [TestCase("/sdk/data/aztables/\n/sdk/data/azappconfig/\n/sdk/data/azcosmos/", "/sdk/data/azappconfig/\n/sdk/data/azcosmos/\n/sdk/data/aztables/", 
            Description = "SDK data service directories should be sorted alphabetically")]
        [TestCase("/sdk/internal/\n/sdk/azidentity/\n/sdk/azcore/\n/sdk/ai", "/sdk/ai\n/sdk/azcore/\n/sdk/azidentity/\n/sdk/internal/", 
            Description = "Mixed SDK directories should be sorted alphabetically")]
        [TestCase("/eng/tools/internal\n/.github/CODEOWNERS\n/eng/emitter-package.json\n/\n/eng/common/pipelines/codeowners-linter.yml\n/eng/tools/generator", "/.github/CODEOWNERS\n/eng/common/pipelines/codeowners-linter.yml\n/eng/emitter-package.json\n/eng/tools/generator\n/eng/tools/internal\n/", 
            Description = "Complex mixed paths with root directory")]
        [TestCase("/sdk/tracing/azotel\n/sdk/template/", "/sdk/template/\n/sdk/tracing/azotel", 
            Description = "Two SDK service directories should be sorted alphabetically")]
        [TestCase("/sdk/azcore/\n/sdk/azcore/\n/sdk/azidentity/", "/sdk/azcore/\n/sdk/azcore/\n/sdk/azidentity/", 
            Description = "Duplicate entries should be preserved and sorted correctly")]
        [TestCase("/sdk/samples/\n/sdk/\n/", "/sdk/\n/sdk/samples/\n/", 
            Description = "Parent directory and child directory with root")]
        // Additional cases: SDK catch-all, wildcard vs exact, double-wildcard, and whitespace path
        [TestCase("/sdk/**\n/sdk/*\n/sdk/storage/", "/sdk/**\n/sdk/*\n/sdk/storage/", Description = "SDK catch-all then wildcard then specific path")]
        [TestCase("/sdk/storage*\n/sdk/storage", "/sdk/storage*\n/sdk/storage", Description = "Wildcard should come before exact path")]
        [TestCase("/**/*Storage*/\n/sdk/storage", "/**/*Storage*/\n/sdk/storage", Description = "Double-wildcard pattern before exact path")]
        [TestCase("   \n/sdk/test/", "/sdk/test/\n   ", Description = "Whitespace path preserved and handled")]
        public void TestPathSorting(string inputPaths, string expectedOutput)
        {
            // Arrange - Parse input paths and create CodeownersEntry objects
            var paths = inputPaths.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var entries = paths.Select(path => new CodeownersEntry { PathExpression = path }).ToArray();
            
            // Act - Sort using the comparer
            var sortedEntries = entries.OrderBy(entry => entry, comparer).ToArray();
            
            // Extract sorted paths
            var sortedPaths = sortedEntries.Select(entry => entry.PathExpression).ToArray();
            var actualOutput = string.Join('\n', sortedPaths);
            
            // Assert
            Assert.That(actualOutput, Is.EqualTo(expectedOutput),
                $"Path sorting failed.\nInput: {inputPaths}\nExpected: {expectedOutput}\nActual: {actualOutput}");
        }

        /// <summary>
        /// Test edge cases for the path comparer
        /// </summary>
        [Test]
        public void TestEmptyPathList()
        {
            var entries = new CodeownersEntry[0];
            var sortedEntries = entries.OrderBy(entry => entry, comparer).ToArray();
            Assert.That(sortedEntries.Length, Is.EqualTo(0), "Empty list should remain empty after sorting");
        }

        [Test]
        public void TestSinglePath()
        {
            var entries = new[] { new CodeownersEntry { PathExpression = "/sdk/test/" } };
            var sortedEntries = entries.OrderBy(entry => entry, comparer).ToArray();
            Assert.That(sortedEntries.Length, Is.EqualTo(1), "Single entry list should have one element");
            Assert.That(sortedEntries[0].PathExpression, Is.EqualTo("/sdk/test/"), "Single entry should be unchanged");
        }

        [Test]
        public void TestIdenticalPaths()
        {
            var entries = new[]
            {
                new CodeownersEntry { PathExpression = "/sdk/test/" },
                new CodeownersEntry { PathExpression = "/sdk/test/" },
                new CodeownersEntry { PathExpression = "/sdk/test/" }
            };
            var sortedEntries = entries.OrderBy(entry => entry, comparer).ToArray();
            Assert.That(sortedEntries.Length, Is.EqualTo(3), "All identical entries should be preserved");
            Assert.IsTrue(sortedEntries.All(e => e.PathExpression == "/sdk/test/"), "All entries should have the same path");
        }

        /// <summary>
        /// Test that the comparer handles null entries gracefully
        /// </summary>
        [Test]
        public void TestNullPathExpression()
        {
            var entries = new[]
            {
                new CodeownersEntry { PathExpression = "/sdk/test/" },
                new CodeownersEntry { PathExpression = null },
                new CodeownersEntry { PathExpression = "/eng/test/" }
            };
            
            // This should not throw an exception
            Assert.DoesNotThrow(() =>
            {
                var sortedEntries = entries.OrderBy(entry => entry, comparer).ToArray();
                Assert.That(sortedEntries.Length, Is.EqualTo(3), "All entries should be preserved even with null paths");
            });
        }
    }
}
