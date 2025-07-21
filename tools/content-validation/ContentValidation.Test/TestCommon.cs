using Microsoft.Playwright;
using System.Text.Json;
using UtilityLibraries;
using ReportHelper;
using System.Collections.Concurrent;

namespace ContentValidation.Test
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class TestCommon
    {
        public static List<string> TestLinksOfCodeFormatValidation { get; set; }
        public static List<string> TestLinksOfEmptyTagsValidation { get; set; }
        public static List<string> TestLinksOfErrorDisplayValidation { get; set; }
        public static List<string> TestLinksOfExtraLabelValidation { get; set; }
        public static List<string> TestLinksOfGarbledTextValidation { get; set; }
        public static List<string> TestLinksOfInconsistentTextFormatValidation { get; set; }
        public static List<string> TestLinksOfInvalidTagsValidation { get; set; }
        public static List<string> TestLinksOfMissingContentValidation { get; set; }
        public static List<string> TestLinksOfTypeAnnotationValidation { get; set; }
        public static List<string> TestLinksOfUnnecessarySymbolsValidation { get; set; }

        public static ConcurrentQueue<TResult> TestCommonResults = new ConcurrentQueue<TResult>();

        public static IPlaywright playwright;
        static TestCommon()
        {
            playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
            TestLinksOfCodeFormatValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/dotnet/api/azure.ai.metricsadvisor.administration?view=azure-dotnet"
            };
            TestLinksOfEmptyTagsValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/dotnet/api/azure.ai.metricsadvisor.administration.azureblobdatafeedsource?view=azure-dotnet"
            };
            TestLinksOfErrorDisplayValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/javascript/api/@azure/app-configuration/optionallabelsfields?view=azure-node-latest"
            };
            TestLinksOfExtraLabelValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/java/api/com.azure.data.tables.tableserviceasyncclient?view=azure-java-stable"
            };
            TestLinksOfGarbledTextValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/java/api/com.azure.data.tables.tableserviceasyncclient?view=azure-java-stable"
            };
            TestLinksOfInconsistentTextFormatValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/java/api/com.azure.data.tables.models.listentitiesoptions?view=azure-java-stable",
            };
            TestLinksOfInvalidTagsValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/python/api/azure-ai-language-questionanswering/azure.ai.language.questionanswering.authoring.aio.authoringclient?view=azure-python&branch=main"
            };
            TestLinksOfMissingContentValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/python/api/azure-ai-contentsafety/azure.ai.contentsafety.models.addorupdatetextblocklistitemsoptions?view=azure-python&branch=main"
            };
            TestLinksOfTypeAnnotationValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/python/api/azure-ai-contentsafety/azure.ai.contentsafety.models.addorupdatetextblocklistitemsoptions?view=azure-python&branch=main"
            };
            TestLinksOfUnnecessarySymbolsValidation = new List<string>()
            {
                "https://learn.microsoft.com/en-us/python/api/azure-monitor-query/azure.monitor.query.aio.logsqueryclient?view=azure-python&branch=main"
            };
        }

        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfCodeFormatValidation))]
        public async Task TestCodeFormatValidation(string testLink)
        {

            IValidation Validation = new CodeFormatValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestCodeFormat";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfEmptyTagsValidation))]
        public async Task TestEmptyTagsValidation(string testLink)
        {

            IValidation Validation = new EmptyTagsValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestEmptyTags";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfErrorDisplayValidation))]
        public async Task TestErrorDisplayValidation(string testLink)
        {

            IValidation Validation = new ErrorDisplayValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestErrorDisplay";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfExtraLabelValidation))]
        public async Task TestExtraLabelValidation(string testLink)
        {

            IValidation Validation = new ExtraLabelValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestExtraLabel";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfGarbledTextValidation))]
        public async Task TestGarbledTextValidation(string testLink)
        {

            IValidation Validation = new GarbledTextValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestGarbledText";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfInconsistentTextFormatValidation))]
        public async Task TestInconsistentTextFormatValidation(string testLink)
        {

            IValidation Validation = new InconsistentTextFormatValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestInconsistentTextFormat";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfInvalidTagsValidation))]
        public async Task TestInvalidTagsValidation(string testLink)
        {

            IValidation Validation = new InvalidTagsValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestInvalidTags";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfMissingContentValidation))]
        public async Task TestMissingContentValidation(string testLink)
        {

            IValidation Validation = new MissingContentValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestMissingContent";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfTypeAnnotationValidation))]
        public async Task TestMissingTypeAnnotationValidation(string testLink)
        {

            IValidation Validation = new TypeAnnotationValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestMissingTypeAnnotation";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }


        [Test]
        [Category("CommonTest")]
        [TestCaseSource(nameof(TestLinksOfUnnecessarySymbolsValidation))]
        public async Task TestUnnecessarySymbolsValidation(string testLink)
        {

            IValidation Validation = new UnnecessarySymbolsValidation(playwright);

            var res = new TResult();

            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestUnnecessarySymbols";
                if (!res.Result)
                {
                    TestCommonResults.Enqueue(res);
                }
            }
            catch
            {
                throw;
            }

            Assert.That(res.Result, Is.True, res.FormatErrorMessage());
        }
    }
}

