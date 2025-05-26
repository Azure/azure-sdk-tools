using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Text.Json;
using UtilityLibraries;
using ReportHelper;

namespace ContentValidation.Test
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class TestPageLabel
    {
        public static List<string> TestLinks { get; set; }

        public static ConcurrentQueue<TResult> TestExtraLabelResults = new ConcurrentQueue<TResult>();

        public static ConcurrentQueue<TResult> TestUnnecessarySymbolsResults = new ConcurrentQueue<TResult>();

        public static ConcurrentQueue<TResult> TestInvalidTagsResults = new ConcurrentQueue<TResult>();

        public static ConcurrentQueue<TResult> TestCodeFormatResults = new ConcurrentQueue<TResult>();

        public static IPlaywright playwright;

        static TestPageLabel()
        {
            playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
            TestLinks = JsonSerializer.Deserialize<List<string>>(File.ReadAllText("../../../appsettings.json")) ?? new List<string>();
        }

        [OneTimeTearDown]
        public void SaveTestData()
        {
            playwright?.Dispose();

            string excelFilePath = ConstData.TotalIssuesExcelFileName;
            string sheetName = "TotalIssues";
            string jsonFilePath = ConstData.TotalIssuesJsonFileName;

            ExcelHelper4Test.AddTestResult(TestExtraLabelResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestUnnecessarySymbolsResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestInvalidTagsResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestCodeFormatResults, excelFilePath, sheetName);
            JsonHelper4Test.AddTestResult(TestExtraLabelResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestUnnecessarySymbolsResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestInvalidTagsResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestCodeFormatResults, jsonFilePath);
        }

        [Test]
        [Category("PythonTest")]
        [Category("JavaTest")]
        [Category("JsTest")]
        [TestCaseSource(nameof(TestLinks))]
        public async Task TestExtraLabel(string testLink)
        {

            IValidation Validation = new ExtraLabelValidation(playwright);

            var res = new TResult();
            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestExtraLabel";
                if (!res.Result)
                {
                    TestExtraLabelResults.Enqueue(res);
                }
                pipelineStatusHelper.SavePipelineFailedStatus("ExtraLabelValidation", "succeed");
            }
            catch
            {
                pipelineStatusHelper.SavePipelineFailedStatus("ExtraLabelValidation", "failed");
                throw;
            }

            Assert.That(res.Result, res.FormatErrorMessage());


        }

        [Test]
        [Category("PythonTest")]
        [Category("JavaTest")]
        [Category("JsTest")]
        [TestCaseSource(nameof(TestLinks))]
        public async Task TestUnnecessarySymbols(string testLink)
        {

            var res = new TResult();
            try
            {

                IValidation Validation = new UnnecessarySymbolsValidation(playwright);

                res = await Validation.Validate(testLink);

                res.TestCase = "TestUnnecessarySymbols";
                if (!res.Result)
                {
                    TestUnnecessarySymbolsResults.Enqueue(res);
                }
                pipelineStatusHelper.SavePipelineFailedStatus("UnnecessarySymbolsValidation", "succeed");
            }
            catch
            {
                pipelineStatusHelper.SavePipelineFailedStatus("UnnecessarySymbolsValidation", "failed");
                throw;
            }

            Assert.That(res.Result, res.FormatErrorMessage());


        }

        [Test]
        [Category("JavaTest")]
        [TestCaseSource(nameof(TestLinks))]
        public async Task TestInvalidTags(string testLink)
        {

            var res = new TResult();
            try
            {

                IValidation Validation = new InvalidTagsValidation(playwright);

                res = await Validation.Validate(testLink);

                res.TestCase = "TestInvalidTags";
                if (!res.Result)
                {
                    TestInvalidTagsResults.Enqueue(res);
                }
                pipelineStatusHelper.SavePipelineFailedStatus("InvalidTagsValidation", "succeed");
            }
            catch
            {
                pipelineStatusHelper.SavePipelineFailedStatus("InvalidTagsValidation", "failed");
                throw;
            }

            Assert.That(res.Result, res.FormatErrorMessage());


        }
        
        [Test]
        [Category("JavaTest")]
        [TestCaseSource(nameof(TestLinks))]
        public async Task TestCodeFormat(string testLink)
        {

            var res = new TResult();
            try
            {

                IValidation Validation = new CodeFormatValidation(playwright);

                res = await Validation.Validate(testLink);

                res.TestCase = "TestCodeFormat";
                if (!res.Result)
                {
                    TestCodeFormatResults.Enqueue(res);
                }
                pipelineStatusHelper.SavePipelineFailedStatus("CodeFormatValidation", "succeed");
            }
            catch
            {
                pipelineStatusHelper.SavePipelineFailedStatus("CodeFormatValidation", "failed");
                throw;
            }

            Assert.That(res.Result, res.FormatErrorMessage());


        }
    }
}