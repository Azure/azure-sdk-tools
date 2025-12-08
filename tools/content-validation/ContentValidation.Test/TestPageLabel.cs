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

        public static ConcurrentQueue<TResult> TestMissingGenericsResults = new ConcurrentQueue<TResult>();

        public static IPlaywright playwright;
        public static IBrowser browser;

        static TestPageLabel()
        {
            playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
            // Create a shared browser instance for all tests
            browser = playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).GetAwaiter().GetResult();
            TestLinks = JsonSerializer.Deserialize<List<string>>(File.ReadAllText("../../../../../tools/content-validation/ContentValidation.Test/appsettings.json")) ?? new List<string>();
        }

        [OneTimeTearDown]
        public void SaveTestData()
        {
            browser?.CloseAsync().GetAwaiter().GetResult();
            playwright?.Dispose();

            string excelFilePath = ConstData.TotalIssuesExcelFileName;
            string sheetName = "TotalIssues";
            string jsonFilePath = ConstData.TotalIssuesJsonFileName;

            ExcelHelper4Test.AddTestResult(TestExtraLabelResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestUnnecessarySymbolsResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestMissingGenericsResults, excelFilePath, sheetName);
            JsonHelper4Test.AddTestResult(TestExtraLabelResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestUnnecessarySymbolsResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestMissingGenericsResults, jsonFilePath);
        }

        [Test]
        [Category("PythonTest")]
        [Category("JavaTest")]
        [Category("JsTest")]
        [TestCaseSource(nameof(TestLinks))]
        public async Task TestExtraLabel(string testLink)
        {
            IValidation Validation = new ExtraLabelValidation(browser);

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

                IValidation Validation = new UnnecessarySymbolsValidation(browser);

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
        public async Task TestMissingGenerics(string testLink)
        {
            var res = new TResult();
            try
            {

                IValidation Validation = new MissingGenericsValidation(browser);

                res = await Validation.Validate(testLink);

                res.TestCase = "TestMissingGenerics";
                if (!res.Result)
                {
                    TestMissingGenericsResults.Enqueue(res);
                }
                pipelineStatusHelper.SavePipelineFailedStatus("MissingGenericsValidation", "succeed");
            }
            catch
            {
                pipelineStatusHelper.SavePipelineFailedStatus("MissingGenericsValidation", "failed");
                throw;
            }

            Assert.That(res.Result, res.FormatErrorMessage());
        }
    }
}
