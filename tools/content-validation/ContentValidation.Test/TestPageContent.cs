using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Text.Json;
using UtilityLibraries;
using ReportHelper;

namespace ContentValidation.Test
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class TestPageContent
    {
        public static List<string> TestLinks { get; set; }
        public static List<string> DuplicateTestLink { get; set; }

        public static ConcurrentQueue<TResult> TestTableMissingContentResults = new ConcurrentQueue<TResult>();

        public static ConcurrentQueue<TResult> TestGarbledTextResults = new ConcurrentQueue<TResult>();

        public static ConcurrentQueue<TResult> TestDuplicateServiceResults = new ConcurrentQueue<TResult>();

        public static ConcurrentQueue<TResult> TestInconsistentTextFormatResults = new ConcurrentQueue<TResult>();

        public static ConcurrentQueue<TResult> TestErrorDisplayResults = new ConcurrentQueue<TResult>();

        public static ConcurrentQueue<TResult> TestEmptyTagsResults = new ConcurrentQueue<TResult>();

        public static IPlaywright playwright;
        public static IBrowser browser;

        static TestPageContent()
        {
            playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
            // Create a shared browser instance for all tests
            browser = playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).GetAwaiter().GetResult();
            TestLinks = JsonSerializer.Deserialize<List<string>>(File.ReadAllText("../../../../../tools/content-validation/ContentValidation.Test/appsettings.json")) ?? new List<string>();

            //This list is for testing duplicate services.
            DuplicateTestLink = new List<string>
            {
                "https://learn.microsoft.com/en-us/python/api/overview/azure/?view=azure-python"
            };
        }


        [OneTimeTearDown]
        public void SaveTestData()
        {
            browser?.CloseAsync().GetAwaiter().GetResult();
            playwright?.Dispose();
            string excelFilePath = ConstData.TotalIssuesExcelFileName;
            string sheetName = "TotalIssues";
            string jsonFilePath = ConstData.TotalIssuesJsonFileName;
            ExcelHelper4Test.AddTestResult(TestTableMissingContentResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestGarbledTextResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestDuplicateServiceResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestInconsistentTextFormatResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestErrorDisplayResults, excelFilePath, sheetName);
            ExcelHelper4Test.AddTestResult(TestEmptyTagsResults, excelFilePath, sheetName);
            JsonHelper4Test.AddTestResult(TestTableMissingContentResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestGarbledTextResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestDuplicateServiceResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestInconsistentTextFormatResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestErrorDisplayResults, jsonFilePath);
            JsonHelper4Test.AddTestResult(TestEmptyTagsResults, jsonFilePath);
        }


        // [Test]
        // [Category("PythonTest")]
        // [Category("JavaTest")]
        // [Category("JsTest")]
        // [Category("DotNetTest")]
        // [TestCaseSource(nameof(TestLinks))]
        // public async Task TestTableMissingContent(string testLink)
        // {
        //     IValidation Validation = new MissingContentValidation(browser);

        //     var res = new TResult();
        //     try
        //     {
        //         res = await Validation.Validate(testLink);
        //         res.TestCase = "TestTableMissingContent";
        //         if (!res.Result)
        //         {
        //             TestTableMissingContentResults.Enqueue(res);
        //         }
        //         pipelineStatusHelper.SavePipelineFailedStatus("MissingContentValidation", "succeed");
        //     }
        //     catch
        //     {
        //         pipelineStatusHelper.SavePipelineFailedStatus("MissingContentValidation", "failed");
        //         throw;
        //     }

        //     Assert.That(res.Result, res.FormatErrorMessage());
        // }

        // [Test]
        // [Category("PythonTest")]
        // [Category("JavaTest")]
        // [Category("JsTest")]
        // [TestCaseSource(nameof(TestLinks))]
        // public async Task TestGarbledText(string testLink)
        // {
        //     IValidation Validation = new GarbledTextValidation(browser);

        //     var res = new TResult();
        //     try
        //     {
        //         res = await Validation.Validate(testLink);
        //         res.TestCase = "TestGarbledText";
        //         if (!res.Result)
        //         {
        //             TestGarbledTextResults.Enqueue(res);
        //         }
        //         pipelineStatusHelper.SavePipelineFailedStatus("GarbledTextValidation", "succeed");
        //     }
        //     catch
        //     {
        //         pipelineStatusHelper.SavePipelineFailedStatus("GarbledTextValidation", "failed");
        //         throw;
        //     }

        //     Assert.That(res.Result, res.FormatErrorMessage());
        // }


        // [Test]
        // [Category("JavaTest")]
        // [Category("JsTest")]
        // [TestCaseSource(nameof(TestLinks))]
        // public async Task TestInconsistentTextFormat(string testLink)
        // {
        //     IValidation Validation = new InconsistentTextFormatValidation(browser);

        //     var res = new TResult();
        //     try
        //     {
        //         res = await Validation.Validate(testLink);
        //         res.TestCase = "TestInconsistentTextFormat";
        //         if (!res.Result)
        //         {
        //             TestInconsistentTextFormatResults.Enqueue(res);
        //         }
        //         pipelineStatusHelper.SavePipelineFailedStatus("InconsistentTextFormatValidation", "succeed");
        //     }
        //     catch
        //     {
        //         pipelineStatusHelper.SavePipelineFailedStatus("InconsistentTextFormatValidation", "failed");
        //         throw;
        //     }

        //     Assert.That(res.Result, res.FormatErrorMessage());
        // }


        // [Test]
        // [Category("JsTest")]
        // [TestCaseSource(nameof(TestLinks))]
        // public async Task TestErrorDisplay(string testLink)
        // {
        //     IValidation Validation = new ErrorDisplayValidation(browser);

        //     var res = new TResult();
        //     try
        //     {
        //         res = await Validation.Validate(testLink);
        //         res.TestCase = "TestErrorDisplay";
        //         if (!res.Result)
        //         {
        //             TestDuplicateServiceResults.Enqueue(res);
        //         }
        //         pipelineStatusHelper.SavePipelineFailedStatus("ErrorDisplayValidation", "succeed");
        //     }
        //     catch
        //     {
        //         pipelineStatusHelper.SavePipelineFailedStatus("ErrorDisplayValidation", "failed");
        //         throw;
        //     }

        //     Assert.That(res.Result, res.FormatErrorMessage());
        // }


        [Test]
        [Category("DotNetTest")]
        [TestCaseSource(nameof(TestLinks))]
        public async Task TestEmptyTags(string testLink)
        {
            IValidation Validation = new EmptyTagsValidation(browser);

            var res = new TResult();
            try
            {
                Console.WriteLine($"Starting EmptyTagsValidation for {testLink}");
                res = await Validation.Validate(testLink);
                res.TestCase = "TestEmptyTags";
                if (!res.Result)
                {
                    TestEmptyTagsResults.Enqueue(res);
                }
                pipelineStatusHelper.SavePipelineFailedStatus("EmptyTagsValidation", "succeed");
                Console.WriteLine($"Completed EmptyTagsValidation for {testLink}");
            }
            catch
            {
                pipelineStatusHelper.SavePipelineFailedStatus("EmptyTagsValidation", "failed");
                throw;
            }

            Assert.That(res.Result, res.FormatErrorMessage());
        }


        [Test]
        [Category("SpecialTest")]
        [TestCaseSource(nameof(DuplicateTestLink))]
        public async Task TestDuplicateService(string testLink)
        {
            IValidation Validation = new DuplicateServiceValidation(browser);

            var res = new TResult();
            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestDuplicateService";
                if (!res.Result)
                {
                    TestDuplicateServiceResults.Enqueue(res);
                }
                pipelineStatusHelper.SavePipelineFailedStatus("DuplicateServiceValidation", "succeed");
            }
            catch
            {
                pipelineStatusHelper.SavePipelineFailedStatus("DuplicateServiceValidation", "failed");
                throw;
            }

            Assert.That(res.Result, res.FormatErrorMessage());
        }
    }
}
