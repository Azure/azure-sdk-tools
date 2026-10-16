using System;
using System.Collections.Generic;
using System.IO;
using Azure.Sdk.Tools.CodeownersMigration.Verification;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersMigration.Tests
{
    /// <summary>
    /// The loader is what stands between an ordinal comparison and a meaningless one. When the parser drops
    /// a malformed block, every entry after it shifts position, and a block dropped from *both* files would
    /// let the run pass while checking nothing about it. Both directions are covered here: the guard must
    /// fire on a rejected block, and it must not fire on a good file.
    ///
    /// Marked non-parallelizable because the guard redirects the process-global Console.Error.
    /// </summary>
    [NonParallelizable]
    public class CodeownersFileLoaderTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "codeowners-migration-tests", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private string WriteFile(string name, params string[] lines)
        {
            string path = Path.Combine(_directory, name);
            File.WriteAllLines(path, lines);
            return path;
        }

        [Test]
        public void WellFormedFileLoads()
        {
            string path = WriteFile(
                "CODEOWNERS",
                "# A hand-written banner",
                "",
                "# PRLabel: %Tables",
                "/sdk/tables/    @alice @bob",
                "",
                "# ServiceLabel: %Storage",
                "# ServiceOwners: @carol");

            List<CodeownersEntry> entries = CodeownersFileLoader.Load(path, null);

            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].PathExpression, Is.EqualTo("/sdk/tables/"));
        }

        [Test]
        public void RejectedBlockFailsTheLoad()
        {
            // A PRLabel moniker must be part of a block ending in a source path/owner line. The parser
            // reports this on stderr and drops the entry, which would shift every following index.
            string path = WriteFile(
                "CODEOWNERS",
                "# PRLabel: %Orphan",
                "",
                "/sdk/tables/    @alice");

            var exception = Assert.Throws<CodeownersLoadException>(() => CodeownersFileLoader.Load(path, null));
            Assert.That(exception.Message, Does.Contain(path));
            Assert.That(exception.Message, Does.Contain("PRLabel"));
        }

        [Test]
        public void MissingFileFailsTheLoad()
        {
            string path = Path.Combine(_directory, "does-not-exist");

            var exception = Assert.Throws<CodeownersLoadException>(() => CodeownersFileLoader.Load(path, null));
            Assert.That(exception.Message, Does.Contain("not found"));
        }

        [Test]
        public void StdErrIsRestoredAfterLoading()
        {
            // If the redirect leaked, later diagnostics would vanish and a subsequent load would inherit a
            // stale writer, making the rejected-block guard fire on unrelated output.
            TextWriter before = Console.Error;

            CodeownersFileLoader.Load(WriteFile("CODEOWNERS", "/sdk/tables/    @alice"), null);

            Assert.That(Console.Error, Is.SameAs(before));
        }

        [Test]
        public void StdErrIsRestoredAfterARejectedBlock()
        {
            TextWriter before = Console.Error;

            Assert.Throws<CodeownersLoadException>(
                () => CodeownersFileLoader.Load(WriteFile("CODEOWNERS", "# PRLabel: %Orphan", ""), null));

            Assert.That(Console.Error, Is.SameAs(before));
        }
    }
}
