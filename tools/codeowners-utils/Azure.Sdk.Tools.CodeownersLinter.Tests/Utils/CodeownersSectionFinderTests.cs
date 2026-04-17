using System.Collections.Generic;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    [TestFixture]
    public class CodeownersSectionFinderTests
    {

        private List<string> clientLibrariesExample = new List<string>
        {
            "# Some other section",
            "file1.cs @owner1",
            "###",                  // Section start
            "# Client Libraries",   // (Section name)
            "###",                  // Header end
            "file2.cs @owner2",     // First line of content
            "file3.cs @owner3",
            "###",                  // Section ends
            "# Another section",
            "###",
            "file4.cs @owner4",
            "file5.cs @owner5",
        };

        [Test]
        public void FindSection_LocatesSection()
        {
            var result = CodeownersSectionFinder.FindSection(clientLibrariesExample, "Client Libraries");

            Assert.That(result.headerStart, Is.EqualTo(2));
            Assert.That(result.contentStart, Is.EqualTo(5));
            Assert.That(result.sectionEnd, Is.EqualTo(7));
        }

        [Test]
        public void FindSection_ReturnsNotFound()
        {
            var result = CodeownersSectionFinder.FindSection(clientLibrariesExample, "Nonexistent Section");

            Assert.That(result.headerStart, Is.EqualTo(-1));
            Assert.That(result.contentStart, Is.EqualTo(-1));
            Assert.That(result.sectionEnd, Is.EqualTo(-1));
        }

        [Test]
        public void FindNextSectionStart_ReturnsEnd()
        {
            var result = CodeownersSectionFinder.FindNextSectionStart(clientLibrariesExample, 10);

            Assert.That(result, Is.EqualTo(clientLibrariesExample.Count));
        }
    }
}
