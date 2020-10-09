using CreateRuleFabricBot;
using CreateRuleFabricBot.Rules.PullRequestLabel;
using CreateRuleFabricBotTests;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Tests
{
    public class CodeOwnerTests
    {
        [DatapointSource]
        public TestAndExpected[] Expectations = new TestAndExpected[]
            {
                new TestAndExpected("/test @author", "# PRLabel: %label", "/test", new []{ "author"}, new[]{"label" }),
                new TestAndExpected("/test @author1 @author2", "# PRLabel: %label", "/test", new []{ "author1", "author2"}, new[]{"label" }),
                new TestAndExpected("/test @author", "# PRLabel: %label1 %label2", "/test", new []{ "author"}, new[]{"label1", "label2" }),
                // no labels
                new TestAndExpected("/test @author", "", "/test", new []{ "author"}, new string[]{ }),

                // Different casing for PRLabel marker
                new TestAndExpected("/test @author1 @author2", "# prlabeL  : %label", "/test", new []{ "author1", "author2"}, new[]{"label" }),

                // >>>> malformed lines <<<<
                // no colon after PRLabel
                new TestAndExpected("/test @author", "# PRLabel %label1 %label2", "/test", new []{ "author"}, new string[]{  }),
                // no PRLabel marker
                new TestAndExpected("/test @author", "# : %label1 %label2", "/test", new []{ "author"}, new string[]{  }),
                // Empty label name
                new TestAndExpected("/test @author", "#PRLabel : % %label1 %label2", "/test", new []{ "author"}, new string[]{ "label1", "label2" }),
                // Empty author name
                new TestAndExpected("/test @ @author", "#PRLabel : % %label1 %label2", "/test", new []{ "author"}, new string[]{ "label1", "label2" })
            };


        [Theory]
        public void ValidateOwnersLines(TestAndExpected entry)
        {
            // create the content
            string content = $"{entry.LabelsLine}\r\n{entry.PathAndOwners}";

            CodeOwnerEntry coe = CodeOwnersFile.ParseContent(content).First();
            Assert.AreEqual(entry.ExpectedLabels, coe.PRLabels);
            Assert.AreEqual(entry.ExpectedOwners, coe.Owners);
            Assert.AreEqual(entry.ExpectedPath, coe.PathExpression);
        }

        [Test]
        public void ParseInvalidEntry()
        {
            // no path and owners
            var entry = new TestAndExpected("", "# PRLabel: %label1 %label2", string.Empty, new string[] { }, new string[] { "label1", "label2" });

            // create the content
            string content = $"{entry.LabelsLine}\r\n{entry.PathAndOwners}";

            CodeOwnerEntry coe = CodeOwnersFile.ParseContent(content).FirstOrDefault();

            Assert.IsNull(coe);
        }

        [Test]
        public void ValidateCodeOwnersContent()
        {
            string content = @"#Comment


# ServiceLabel: %F1 %Service Attention
# PRLabel: %F1
/folder1/                                @user1

# ServiceLabel: %F2 %Service Attention
# PRLabel: %F2
/folder2/                                @user2

# ServiceLabel: %Service Attention %F3
/folder3/                                   @user3 @user1

# PRLabel: %F4 %Service Attention
/folder4/                                   @user4

/folder5/                                   @user5

# ServiceLabel: %MyService
#/<NotInRepo>/           @user6


# ServiceLabel: %MyService
# CommentedLine           @user7


/folder6            @user7


# ServiceLabel: %MyService
/folder8           @user6  #This has comment at the end
";

            List<CodeOwnerEntry> entries = CodeOwnersFile.ParseContent(content);
            Assert.AreEqual(8, entries.Count);


            Assert.AreEqual("F1", entries[0].PRLabels[0]);
            Assert.AreEqual("F1", entries[0].ServiceLabels[0]);
            Assert.AreEqual("Service Attention", entries[0].ServiceLabels[1]);

            Assert.AreEqual("F2", entries[1].PRLabels[0]);
            Assert.AreEqual("F2", entries[1].ServiceLabels[0]);
            Assert.AreEqual("Service Attention", entries[1].ServiceLabels[1]);
            Assert.AreEqual("/folder2/", entries[1].PathExpression);

            Assert.AreEqual("Service Attention", entries[2].ServiceLabels[0]);
            Assert.AreEqual(0, entries[2].PRLabels.Count);
            Assert.AreEqual("F3", entries[2].ServiceLabels[1]);

            Assert.AreEqual("F4", entries[3].PRLabels[0]);
            Assert.AreEqual(0, entries[3].ServiceLabels.Count);
            Assert.AreEqual("Service Attention", entries[3].PRLabels[1]);

            Assert.AreEqual(0, entries[4].ServiceLabels.Count);
            Assert.AreEqual(0, entries[4].PRLabels.Count);
            Assert.AreEqual("/folder5/", entries[4].PathExpression);

            Assert.AreEqual(1, entries[5].ServiceLabels.Count);
            Assert.AreEqual(0, entries[5].PRLabels.Count);
            Assert.AreEqual("#/<NotInRepo>/", entries[5].PathExpression);

            Assert.AreEqual(1, entries[6].ServiceLabels.Count);
            Assert.AreEqual(0, entries[6].PRLabels.Count);
            Assert.AreEqual("/folder6", entries[6].PathExpression);

            Assert.AreEqual(1, entries[7].ServiceLabels.Count);
            Assert.AreEqual(0, entries[7].PRLabels.Count);
            Assert.AreEqual("/folder8", entries[7].PathExpression);
            Assert.AreEqual("user6", entries[7].Owners[0]);
        }
    }
}