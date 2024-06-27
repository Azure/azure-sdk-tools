using Xunit;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Chrome;
using System.Collections.Generic;
using SeleniumExtras.WaitHelpers;
using System.Linq;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Net.Http;
using System.IO;
using System.Net.Http.Headers;
using System.Threading.Tasks;

#if false
namespace APIViewUITests
{

    public class SmokeTestsFixture : IDisposable
    {
        internal readonly HttpClient _httpClient;
        internal readonly string _uri;
        internal readonly string _testPkgsPath;
        internal readonly string _endpoint;
        internal readonly ChromeOptions _chromeOptions;
        private readonly CosmosClient _cosmosClient;
        private readonly BlobContainerClient _blobCodeFileContainerClient;
        private readonly BlobContainerClient _blobOriginalContainerClient;
        private readonly BlobContainerClient _blobUsageSampleRepository;
        private readonly BlobContainerClient _blobCommentsRepository;

        public SmokeTestsFixture()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix: "APIVIEW_")
                .AddUserSecrets(typeof(SmokeTestsFixture).Assembly)
                .Build();

            _uri = config["Uri"];
            _testPkgsPath = config["TestPkgPath"];
            _endpoint = config["EndPoint"];
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("ApiKey", config["ApiKey"]);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("multipart/form-data"));

            _cosmosClient = new CosmosClient(config["Cosmos:ConnectionString"]);
            var dataBaseResponse = _cosmosClient.CreateDatabaseIfNotExistsAsync("APIView").Result;
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Reviews", "/id");
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Comments", "/ReviewId");
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("Profiles", "/id");
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("PullRequests", "/PullRequestNumber");
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("UsageSamples", "/ReviewId");
            _ = dataBaseResponse.Database.CreateContainerIfNotExistsAsync("UserPreference", "/ReviewId");

            _blobCodeFileContainerClient = new BlobContainerClient(config["Blob:ConnectionString"], "codefiles");
            _blobOriginalContainerClient = new BlobContainerClient(config["Blob:ConnectionString"], "originals");
            _blobUsageSampleRepository = new BlobContainerClient(config["Blob:ConnectionString"], "usagesamples");
            _blobCommentsRepository = new BlobContainerClient(config["Blob:ConnectionString"], "comments");
            _ = _blobCodeFileContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = _blobOriginalContainerClient.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = _blobUsageSampleRepository.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);
            _ = _blobCommentsRepository.CreateIfNotExistsAsync(PublicAccessType.BlobContainer);

            _chromeOptions = new ChromeOptions();
            _chromeOptions.AddArgument("--start-maximized");
            _chromeOptions.AddArgument("--enable-automation");
            _chromeOptions.AddArgument("--ignore-certificate-errors");
            _chromeOptions.AddArgument("--headless");
            _chromeOptions.AddArgument("--no-sandbox");

            // Upload Reviews Automatically
            var cSharpFileName = $"azure.identity.1.9.0-beta.1.nupkg";
            var cSharpFilePath = Path.Combine(_testPkgsPath, cSharpFileName);
            SubmitAPIReview(cSharpFileName, cSharpFilePath, this._uri, "Auto Review - Test");
        }

        private void SubmitAPIReview(string packageName, string filePath, string uri, string apiLabel)
        {
            using (var multiPartFormData = new MultipartFormDataContent())
            {
                var fileInfo = new FileInfo(filePath);
                var fileStreamContent = new StreamContent(File.OpenRead(filePath));
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                multiPartFormData.Add(fileStreamContent, name: "file", fileName: packageName);
                var stringContent = new StringContent(apiLabel);
                multiPartFormData.Add(stringContent, name: "label");

                var response = _httpClient.PostAsync(uri, multiPartFormData);
                response.Result.EnsureSuccessStatusCode();
            }
        }

        public void Dispose()
        {
            _cosmosClient.GetDatabase("APIView").DeleteAsync().Wait();
            _cosmosClient.Dispose();

            _blobCodeFileContainerClient.DeleteIfExists();
            _blobOriginalContainerClient.DeleteIfExists();
            _blobUsageSampleRepository.DeleteIfExists();
            _blobCommentsRepository.DeleteIfExists();
        }
    }

    public class SmokeTests : IClassFixture<SmokeTestsFixture>
    {
        SmokeTestsFixture _fixture;
        const int WaitTime = 60;

        public SmokeTests(SmokeTestsFixture fixture)
        {
            this._fixture = fixture;
        }

        [Fact(Skip = "Test is failing to find element")]
        public void SmokeTest_CSharp()
        {
            var pkgName = "azure.identity";
            var fileAName = $"{pkgName}.1.8.0.nupkg";
            var fileAPath = Path.Combine(_fixture._testPkgsPath, fileAName);

            // Test Manual Upload
            using (IWebDriver driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), _fixture._chromeOptions, TimeSpan.FromSeconds(WaitTime)))
            {
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(WaitTime);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(WaitTime);
                driver.Navigate().GoToUrl(_fixture._endpoint);
               
                var createReviewBtn = driver.FindElement(By.XPath("//button[@data-bs-target='#uploadModel']"));
                createReviewBtn.Click();
                var languageSelect = driver.FindElement(By.Id("review-language-select"));
                var languageSelectElement = new SelectElement(languageSelect);
                languageSelectElement.SelectByText("C#");
                var fileSelector = driver.FindElement(By.Id("uploadReviewFile"));
                fileSelector.SendKeys(fileAPath);
                var uploadBtn = driver.FindElement(By.XPath("//div[@class='modal-footer']/button[@type='submit']"));
                uploadBtn.Click();
                PageErrorAssertion(driver);
            }

            // Test Auto Upload
            using (IWebDriver driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), _fixture._chromeOptions, TimeSpan.FromSeconds(WaitTime)))
            {
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(WaitTime);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(WaitTime);
                driver.Navigate().GoToUrl(_fixture._endpoint);
                Assert.Equal("Reviews - apiview.dev", driver.Title);

                // Select C# language
                var languageSelector = driver.FindElement(By.Id("language-filter-select"));
                var languageSelectElement = new SelectElement(languageSelector);
                languageSelectElement.SelectByText("C#");
                var reviewNames = driver.FindElements(By.ClassName("review-name"));
                Assert.Equal("Reviews - apiview.dev", driver.Title);
                Assert.Equal(2, reviewNames.Count);

                reviewNames[0].Click();
                PageErrorAssertion(driver);

                var conversiationPage = driver.FindElements(By.ClassName("nav-link")).Single(l => (l.Text.Equals("Conversations")));
                conversiationPage.Click();
                PageErrorAssertion(driver);

                var revisionsPage = driver.FindElements(By.ClassName("nav-link")).Single(l => (l.Text.Equals("Revisions")));
                revisionsPage.Click();
                PageErrorAssertion(driver);

                var usageSamplesPage = driver.FindElements(By.ClassName("nav-link")).Single(l => (l.Text.Equals("Usage Samples")));
                usageSamplesPage.Click();
                PageErrorAssertion(driver);
            }
        }

        [Fact(Skip = "Test is failing to find element")]
        public void SmokeTest_Request_Reviewers()
        {
            using (IWebDriver driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), _fixture._chromeOptions, TimeSpan.FromSeconds(WaitTime)))
            {
                driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(WaitTime);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(WaitTime);
                driver.Navigate().GoToUrl(_fixture._endpoint);

                // Select C# language
                var languageSelector = driver.FindElement(By.Id("language-filter-select"));
                var languageSelectElement = new SelectElement(languageSelector);
                languageSelectElement.SelectByText("C#");
                var reviewNames = driver.FindElements(By.ClassName("review-name"));
                Assert.Equal("Reviews - apiview.dev", driver.Title);

                reviewNames[0].Click();
                PageErrorAssertion(driver);

                var requestReviewersCollapse = driver.FindElement(By.Id("requestReviewersCollapse"));
                if (!requestReviewersCollapse.GetAttribute("class").Contains(" show"))
                {
                    var requestReviewersCollapseTrigger = driver.FindElement(By.XPath("//a[@href='#requestReviewersCollapse']"));
                    requestReviewersCollapseTrigger.Click();
                }

                var possibleReviwers = driver.FindElements(By.XPath("//ul[@id='requestReviewersCollapse']/form/li/ul/li[@class='list-group-item']/div/input"));

                foreach(var reviewer in possibleReviwers)
                {
                    Assert.False(reviewer.Selected);
                    reviewer.Click();
                    Assert.True(reviewer.Selected);
                }

                driver.FindElement(By.Id("submitReviewRequest")).Click();
                driver.Navigate().Refresh();

                possibleReviwers = driver.FindElements(By.XPath("//ul[@id='requestReviewersCollapse']/form/li/ul/li[@class='list-group-item']/div/input"));
                foreach (var reviewer in possibleReviwers)
                {
                    Assert.True(reviewer.Selected);
                }
            }
        }

        private void PageErrorAssertion(IWebDriver driver)
        {
            Assert.NotEqual("Error - apiview.dev", driver.Title);
            Assert.NotEqual("Internal Server Error", driver.Title);
        }

        [Fact(Skip = "Test is too Flaky")]
        public void ReviewFilterOptionsWorkWithoutErrors()
        {
            using (IWebDriver driver = new ChromeDriver())
            {
                driver.Manage().Window.Maximize();
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(50));
                driver.Navigate().GoToUrl("http://localhost:5000/");

                // Language Filters Work Without Errors
                var languageSelector = driver.FindElement(By.Id("language-filter-bootstraps-select"));
                var languageSelectElement = new SelectElement(languageSelector);
                List<string> languages = languageSelectElement.Options.Select(c => c.Text).ToList();
                foreach (var language in languages)
                {
                    languageSelector = driver.FindElement(By.Id("language-filter-bootstraps-select"));
                    languageSelectElement = new SelectElement(languageSelector);
                    languageSelectElement.SelectByText(language);
                    var reviewNames = driver.FindElements(By.ClassName("review-name"));
                    if (reviewNames.Any())
                    {
                        wait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("review-name")));
                        reviewNames[0].Click();
                        Assert.NotEqual("Error - apiview.dev", driver.Title);
                        Assert.NotEqual("Internal Server Error", driver.Title);
                        driver.Navigate().Back();
                        driver.FindElement(By.Id("reset-filter-button")).Click();
                    }
                }

                // State Filters Work Without Errors
                var stateSelector = driver.FindElement(By.Id("state-filter-bootstraps-select"));
                var stateSelectElement = new SelectElement(stateSelector);
                List<string> states = stateSelectElement.Options.Select(c => c.Text).ToList();
                foreach (var state in states)
                {
                    stateSelector = driver.FindElement(By.Id("state-filter-bootstraps-select"));
                    stateSelectElement = new SelectElement(stateSelector);
                    stateSelectElement.SelectByText(state);
                    var reviewNames = driver.FindElements(By.ClassName("review-name"));
                    if (reviewNames.Any())
                    {
                        wait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("review-name")));
                        reviewNames[0].Click();
                        Assert.NotEqual("Error - apiview.dev", driver.Title);
                        Assert.NotEqual("Internal Server Error", driver.Title);
                        driver.Navigate().Back();
                        driver.FindElement(By.Id("reset-filter-button")).Click();
                    }
                }

                // Status Filters Work Without Errors
                var statusSelector = driver.FindElement(By.Id("status-filter-bootstraps-select"));
                var statusSelectElement = new SelectElement(statusSelector);
                List<string> statuses = statusSelectElement.Options.Select(c => c.Text).ToList();
                foreach (var status in statuses)
                {
                    statusSelector = driver.FindElement(By.Id("status-filter-bootstraps-select"));
                    statusSelectElement = new SelectElement(statusSelector);
                    statusSelectElement.SelectByText(status);
                    var reviewNames = driver.FindElements(By.ClassName("review-name"));
                    if (reviewNames.Any())
                    {
                        wait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("review-name")));
                        reviewNames[0].Click();
                        Assert.NotEqual("Error - apiview.dev", driver.Title);
                        Assert.NotEqual("Internal Server Error", driver.Title);
                        driver.Navigate().Back();
                        driver.FindElement(By.Id("reset-filter-button")).Click();
                    }
                }

                // Type Filters Work Without Errors
                var typeSelector = driver.FindElement(By.Id("type-filter-bootstraps-select"));
                var typeSelectElement = new SelectElement(typeSelector);
                List<string> types = typeSelectElement.Options.Select(c => c.Text).ToList();
                foreach (var type in types)
                {
                    typeSelector = driver.FindElement(By.Id("type-filter-bootstraps-select"));
                    typeSelectElement = new SelectElement(typeSelector);
                    typeSelectElement.SelectByText(type);
                    var reviewNames = driver.FindElements(By.ClassName("review-name"));
                    if (reviewNames.Any())
                    {
                        wait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("review-name")));
                        reviewNames[0].Click();
                        Assert.NotEqual("Error - apiview.dev", driver.Title);
                        Assert.NotEqual("Internal Server Error", driver.Title);
                        driver.Navigate().Back();
                        driver.FindElement(By.Id("reset-filter-button")).Click();
                    }
                }
            }
        }
    }
}
#endif
