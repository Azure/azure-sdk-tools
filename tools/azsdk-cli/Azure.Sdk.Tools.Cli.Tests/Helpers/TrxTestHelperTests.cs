// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class TrxTestHelperTests
{
    private TrxTestHelper _helper;
    private TempDirectory _tempDir;

    [SetUp]
    public void SetUp()
    {
        _helper = new TrxTestHelper();
        _tempDir = TempDirectory.Create("TrxTestHelperTests");
    }

    [TearDown]
    public void TearDown()
    {
        _tempDir.Dispose();
    }

    private string WriteTestFile(string filename, string content)
    {
        var path = Path.Combine(_tempDir.DirectoryPath, filename);
        File.WriteAllText(path, content);
        return path;
    }

    private const string TrxWithTwoFailures = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
            <Results>
                <UnitTestResult testId="1" testName="TryGetServiceVersion_ParsesAllServiceVersions" outcome="Failed">
                    <Output>
                        <ErrorInfo>
                            <Message>Assert.AreEqual failed. Expected:&lt;2024-07-01&gt;. Actual:&lt;(null)&gt;.</Message>
                            <StackTrace>at Azure.ResourceManager.Tests.ServiceVersionTests.TryGetServiceVersion_ParsesAllServiceVersions() in ServiceVersionTests.cs:line 42</StackTrace>
                        </ErrorInfo>
                    </Output>
                </UnitTestResult>
                <UnitTestResult testId="2" testName="CreateOrUpdate" outcome="Failed">
                    <Output>
                        <ErrorInfo>
                            <Message>Service request failed. Status: 500 (Internal Server Error)</Message>
                            <StackTrace>at Azure.Storage.Blobs.Tests.BlobContainerClientTests.CreateOrUpdate() in BlobContainerClientTests.cs:line 108</StackTrace>
                        </ErrorInfo>
                    </Output>
                </UnitTestResult>
                <UnitTestResult testId="3" testName="ListBlobs" outcome="Passed"/>
            </Results>
        </TestRun>
        """;

    [Test]
    public async Task ParsesTrxFailures()
    {
        var path = WriteTestFile("test-results.trx", TrxWithTwoFailures);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("TryGetServiceVersion_ParsesAllServiceVersions"));
        Assert.That(result.Items[0].ErrorMessage, Does.Contain("Assert.AreEqual failed"));
        Assert.That(result.Items[0].StackTrace, Does.Contain("ServiceVersionTests.cs:line 42"));
        Assert.That(result.Items[0].Outcome, Is.EqualTo("Failed"));
    }

    [Test]
    public async Task ParsesTrxServerError()
    {
        var path = WriteTestFile("test-results.trx", TrxWithTwoFailures);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items[1].TestCaseTitle, Is.EqualTo("CreateOrUpdate"));
        Assert.That(result.Items[1].ErrorMessage, Does.Contain("500 (Internal Server Error)"));
    }

    [Test]
    public async Task GetFailedTestCases_ReturnsOnlyTitles()
    {
        var path = WriteTestFile("test-results.trx", TrxWithTwoFailures);
        var result = await _helper.GetFailedTestCases(path, ct: CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("TryGetServiceVersion_ParsesAllServiceVersions"));
        Assert.That(result.Items[0].ErrorMessage, Is.Null.Or.Empty);
    }

    [Test]
    public async Task GetFailedTestCases_FiltersByTitle()
    {
        var path = WriteTestFile("test-results.trx", TrxWithTwoFailures);
        var result = await _helper.GetFailedTestCases(path, "CreateOrUpdate", CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("CreateOrUpdate"));
    }

    [Test]
    public async Task GetFailedTestCaseData_ReturnsSingleTest()
    {
        var path = WriteTestFile("test-results.trx", TrxWithTwoFailures);
        var result = await _helper.GetFailedTestCaseData(path, "CreateOrUpdate", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.TestCaseTitle, Is.EqualTo("CreateOrUpdate"));
        Assert.That(result.ErrorMessage, Does.Contain("500"));
    }

    [Test]
    public async Task GetFailedTestCaseData_TitleNotFound_ReturnsError()
    {
        var path = WriteTestFile("test-results.trx", TrxWithTwoFailures);
        var result = await _helper.GetFailedTestCaseData(path, "NonExistentTest", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ResponseError, Does.Contain("No failed test run found"));
        Assert.That(result.ResponseError, Does.Contain("NonExistentTest"));
    }

    [Test]
    public async Task ReturnsEmpty_WhenNoFailures()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                <Results>
                    <UnitTestResult testId="1" testName="PassingTest" outcome="Passed"/>
                </Results>
            </TestRun>
            """;

        var path = WriteTestFile("pass.trx", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);
        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    public void Throws_WhenFileNotFound()
    {
        Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _helper.GetFailedTestResults("/nonexistent/file.trx", CancellationToken.None));
    }

    [Test]
    public async Task CanParse_TrxExtension()
    {
        var path = WriteTestFile("test-results.trx", TrxWithTwoFailures);
        Assert.That(await _helper.CanParseAsync(path), Is.True);
    }

    [Test]
    public async Task CanParse_TestRunRootElement()
    {
        var path = WriteTestFile("test-results.xml", TrxWithTwoFailures);
        Assert.That(await _helper.CanParseAsync(path), Is.True);
    }

    [Test]
    public async Task CanParse_ReturnsFalse_ForJUnitContent()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuites><testsuite name="test"><testcase name="t1"/></testsuite></testsuites>
            """;
        var path = WriteTestFile("results.xml", xml);
        Assert.That(await _helper.CanParseAsync(path), Is.False);
    }
}
