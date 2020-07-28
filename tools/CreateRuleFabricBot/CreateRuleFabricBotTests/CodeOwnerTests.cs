using CreateRuleFabricBot.Rules.PullRequestLabel;
using CreateRuleFabricBotTests;
using NUnit.Framework;

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
                // no path and owners
                new TestAndExpected("", "# PRLabel: %label1 %label2", null, new string[]{ }, new string[]{ "label1", "label2" }), 

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
            CodeOwnerEntry coe = new CodeOwnerEntry(entry.PathAndOwners, entry.LabelsLine);

            Assert.AreEqual(entry.ExpectedLabels, coe.Labels);
            Assert.AreEqual(entry.ExpectedOwners, coe.Owners);
            Assert.AreEqual(entry.ExpectedPath, coe.PathExpression);
        }
    }
}