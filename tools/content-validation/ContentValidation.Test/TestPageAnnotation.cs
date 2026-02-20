using Microsoft.Playwright;
using System.Text.Json;
using UtilityLibraries;
using ReportHelper;
using System.Collections.Concurrent;

namespace ContentValidation.Test
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class TestPageAnnotation
    {
        public static ConcurrentQueue<TResult> TestMissingTypeAnnotationResults = new ConcurrentQueue<TResult>();

        public static IPlaywright playwright;
        public static IBrowser browser;

        public static IEnumerable<string> TypeAnnotationTestLinks()
        {
            return LoadLinks("missingtypeannotation");
        }

        private static IEnumerable<string> LoadLinks(string suffix)
        {
            return JsonSerializer.Deserialize<List<string>>(
                File.ReadAllText($"../../../../../tools/content-validation/ContentValidation.Test/appsettings-{suffix}.json")
            ) ?? new List<string>();
        }

        static TestPageAnnotation()
        {
            playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
            // Create a shared browser instance for all tests
            browser = playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).GetAwaiter().GetResult();
        }

        [OneTimeTearDown]
        public void SaveTestData()
        {
            browser?.CloseAsync().GetAwaiter().GetResult();
            playwright?.Dispose();
            string excelFilePath = ConstData.TotalIssuesExcelFileName;
            string sheetName = "TotalIssues";
            string jsonFilePath = ConstData.TotalIssuesJsonFileName;
            ExcelHelper4Test.AddTestResult(TestMissingTypeAnnotationResults, excelFilePath, sheetName);
            JsonHelper4Test.AddTestResult(TestMissingTypeAnnotationResults, jsonFilePath);
        }

        [Test]
        [TestCaseSource(nameof(TypeAnnotationTestLinks))]
        public async Task TestMissingTypeAnnotation(string testLink)
        {
            IValidation Validation = new TypeAnnotationValidation(browser);

            var res = new TResult();
            
            try
            {
                res = await Validation.Validate(testLink);
                res.TestCase = "TestMissingTypeAnnotation";
                if (!res.Result)
                {
                    TestMissingTypeAnnotationResults.Enqueue(res);
                }
                pipelineStatusHelper.SavePipelineFailedStatus("TypeAnnotationValidation", "succeed");
            }
            catch
            {
                pipelineStatusHelper.SavePipelineFailedStatus("TypeAnnotationValidation", "failed");
                throw;
            }

            Assert.That(res.Result, res.FormatErrorMessage());
        }
    }
}
