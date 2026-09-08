using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.CodeownersMigration.Sections;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersMigration.Tests
{
    public class SectionScannerTests
    {
        [Test]
        public void FindsThreeLineBanners()
        {
            var lines = new List<string>
            {
                "####################",
                "# Core Libraries",
                "####################",
                "/sdk/core/    @owner",
                "####################",
                "# Client Libraries",
                "####################",
                "/sdk/ai/    @owner"
            };

            List<CodeownersSection> sections = SectionScanner.FindSections(lines);

            Assert.That(sections.Select(s => s.Name), Is.EqualTo(new[] { "Core Libraries", "Client Libraries" }));
            Assert.That(sections[0].ContentStart, Is.EqualTo(3));
            Assert.That(sections[0].SectionEnd, Is.EqualTo(4));
            Assert.That(sections[1].SectionEnd, Is.EqualTo(8));
        }

        [Test]
        public void FindsCommentHeadings()
        {
            var lines = new List<string>
            {
                "####################",
                "# End-to-End Samples",
                "####################",
                "/samples/    @owner",
                "# ######## Core Libraries ########",
                "/sdk/core/    @owner"
            };

            List<CodeownersSection> sections = SectionScanner.FindSections(lines);

            Assert.That(sections.Select(s => s.Name), Is.EqualTo(new[] { "End-to-End Samples", "Core Libraries" }));
            Assert.That(sections[1].IsCommentHeading, Is.True);

            // The banner section must stop where the comment heading starts, otherwise entries are attributed
            // to the wrong section.
            Assert.That(sections[0].SectionEnd, Is.EqualTo(4));
            Assert.That(sections[1].ContentStart, Is.EqualTo(5));
        }

        [Test]
        public void CommentHeadingsCanBeDisabled()
        {
            var lines = new List<string>
            {
                "####################",
                "# End-to-End Samples",
                "####################",
                "# ######## Core Libraries ########",
                "/sdk/core/    @owner"
            };

            List<CodeownersSection> sections = SectionScanner.FindSections(lines, includeCommentHeadings: false);

            Assert.That(sections.Select(s => s.Name), Is.EqualTo(new[] { "End-to-End Samples" }));
            Assert.That(sections[0].SectionEnd, Is.EqualTo(5));
        }

        [Test]
        public void IgnoresNonHeadingComments()
        {
            var lines = new List<string>
            {
                "# just a comment",
                "# PRLabel: %Azure.Core",
                "/sdk/core/    @owner"
            };

            Assert.That(SectionScanner.FindSections(lines), Is.Empty);
        }
    }
}
