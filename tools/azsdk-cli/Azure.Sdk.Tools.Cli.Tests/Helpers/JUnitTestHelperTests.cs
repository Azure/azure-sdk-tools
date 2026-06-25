// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class JUnitTestHelperTests
{
    private JUnitTestHelper _helper;
    private TempDirectory _tempDir;

    [SetUp]
    public void SetUp()
    {
        _helper = new JUnitTestHelper();
        _tempDir = TempDirectory.Create("JUnitTestHelperTests");
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

    [Test]
    public async Task ParsesJavaTestResults_AssertionFailure()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuite name="com.azure.compute.batch.TaskManagerTests" tests="5" failures="1" errors="0" skipped="0" time="3.2">
                <testcase name="successAfterOneServerErrorDidRetry" classname="com.azure.compute.batch.TaskManagerTests" time="0.8">
                    <failure message="X should have been retried at least once ==&gt; expected: &lt;true&gt; but was: &lt;false&gt;" type="org.opentest4j.AssertionFailedError">
            org.opentest4j.AssertionFailedError: X should have been retried at least once ==> expected: &lt;true&gt; but was: &lt;false&gt;
                at org.junit.jupiter.api.AssertTrue.assertTrue(AssertTrue.java:36)
                at com.azure.compute.batch.TaskManagerTests.successAfterOneServerErrorDidRetry(TaskManagerTests.java:60)
                    </failure>
                </testcase>
                <testcase name="successNoRetry" classname="com.azure.compute.batch.TaskManagerTests" time="0.2"/>
                <testcase name="failureAfterMaxRetries" classname="com.azure.compute.batch.TaskManagerTests" time="1.5"/>
            </testsuite>
            """;

        var path = WriteTestFile("TEST-com.azure.compute.batch.TaskManagerTests.xml", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("com.azure.compute.batch.TaskManagerTests.successAfterOneServerErrorDidRetry"));
        Assert.That(result.Items[0].ErrorMessage, Does.Contain("should have been retried at least once"));
        Assert.That(result.Items[0].StackTrace, Does.Contain("TaskManagerTests.java:60"));
        Assert.That(result.Items[0].Outcome, Is.EqualTo("org.opentest4j.AssertionFailedError"));
    }

    [Test]
    public async Task ParsesJavaTestResults_MissingRecording()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuite name="com.azure.storage.blob.BlobApiTests" tests="3" failures="1" errors="0" time="2.1">
                <testcase name="downloadContentSmartAccessTierHeaders" classname="com.azure.storage.blob.BlobApiTests" time="1.2">
                    <failure message="Test proxy returned a non-successful status code. 404" type="java.lang.RuntimeException">
            java.lang.RuntimeException: Test proxy returned a non-successful status code. 404; response: {"Message":"Recording file path does not exist.","Status":"NotFound"}
                at com.azure.core.test.http.TestProxyPlaybackClient.sendSync(TestProxyPlaybackClient.java:95)
                at com.azure.storage.blob.BlobApiTests.downloadContentSmartAccessTierHeaders(BlobApiTests.java:234)
                    </failure>
                </testcase>
                <testcase name="downloadContent" classname="com.azure.storage.blob.BlobApiTests" time="0.5"/>
                <testcase name="uploadBlob" classname="com.azure.storage.blob.BlobApiTests" time="0.4"/>
            </testsuite>
            """;

        var path = WriteTestFile("TEST-com.azure.storage.blob.BlobApiTests.xml", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("com.azure.storage.blob.BlobApiTests.downloadContentSmartAccessTierHeaders"));
        Assert.That(result.Items[0].ErrorMessage, Does.Contain("404"));
        Assert.That(result.Items[0].StackTrace, Does.Contain("Recording file path does not exist"));
    }

    [Test]
    public async Task ParsesPythonTestResults_AssertionError()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <testsuites>
                <testsuite name="pytest" errors="0" failures="1" skipped="0" tests="5" time="12.3">
                    <testcase classname="tests.test_chat_completions.TestChatCompletions" name="test_build_messages_user_shows_original_value" time="0.8">
                        <failure message="AssertionError: assert 'Told me about violence' == 'Tell me about violence'">
            def test_build_messages_user_shows_original_value(self):
                messages = [{"role": "user", "content": "Tell me about violence"}]
                result = self.client._build_messages(messages)
            &gt;   assert result[0]["content"] == "Tell me about violence"
            E   AssertionError: assert 'Told me about violence' == 'Tell me about violence'

            tests/test_chat_completions.py:142: AssertionError
                        </failure>
                    </testcase>
                    <testcase classname="tests.test_chat_completions.TestChatCompletions" name="test_build_messages_with_system" time="0.2"/>
                </testsuite>
            </testsuites>
            """;

        var path = WriteTestFile("test-results.xml", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("tests.test_chat_completions.TestChatCompletions.test_build_messages_user_shows_original_value"));
        Assert.That(result.Items[0].ErrorMessage, Does.Contain("Told me about violence"));
        Assert.That(result.Items[0].StackTrace, Does.Contain("test_chat_completions.py:142"));
    }

    [Test]
    public async Task ParsesJsTestResults_EncodingBug()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuites name="vitest tests" tests="12" failures="2" errors="0" time="8.2">
                <testsuite name="test/public/04_stacItemTiler.spec.ts" tests="12" failures="2" errors="0" time="8.2">
                    <testcase classname="test/public/04_stacItemTiler.spec.ts" name="STAC Item Tiler operations &gt; should get WMTS capabilities as XML" time="1.5">
                        <failure message="Failed to execute 'atob' on 'Window': The string to be decoded is not correctly encoded." type="InvalidCharacterError">
            InvalidCharacterError: Failed to execute 'atob' on 'Window': The string to be decoded is not correctly encoded.
                at atob (node:buffer:477:13)
                at decodeCapabilities (src/tiler/wmtsParser.ts:45:22)
                at Object.&lt;anonymous&gt; (test/public/04_stacItemTiler.spec.ts:89:24)
                        </failure>
                    </testcase>
                    <testcase classname="test/public/04_stacItemTiler.spec.ts" name="STAC Item Tiler operations &gt; should parse WMTS tile matrix set" time="0.8">
                        <failure message="Failed to execute 'atob' on 'Window': The string to be decoded is not correctly encoded." type="InvalidCharacterError">
            InvalidCharacterError: Failed to execute 'atob' on 'Window': The string to be decoded is not correctly encoded.
                at atob (node:buffer:477:13)
                at decodeCapabilities (src/tiler/wmtsParser.ts:45:22)
                        </failure>
                    </testcase>
                    <testcase classname="test/public/04_stacItemTiler.spec.ts" name="STAC Item Tiler operations &gt; should get tile" time="2.1"/>
                </testsuite>
            </testsuites>
            """;

        var path = WriteTestFile("test-results.xml", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items[0].TestCaseTitle, Does.Contain("STAC Item Tiler operations > should get WMTS capabilities as XML"));
        Assert.That(result.Items[0].ErrorMessage, Does.Contain("atob"));
        Assert.That(result.Items[0].StackTrace, Does.Contain("wmtsParser.ts:45"));
    }

    [Test]
    public async Task ParsesGoTestResults_GoJunitReport()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuites>
                <testsuite tests="3" failures="1" time="4.2" name="github.com/Azure/azure-sdk-for-go/sdk/azcore">
                    <testcase classname="azcore" name="TestNewClient" time="1.0"/>
                    <testcase classname="azcore" name="TestRetryPolicy" time="2.0">
                        <failure message="Failed" type="">
            === RUN   TestRetryPolicy
                client_test.go:85: expected no error, got: context deadline exceeded
            --- FAIL: TestRetryPolicy (2.00s)
                        </failure>
                    </testcase>
                    <testcase classname="azcore" name="TestPipeline" time="1.2"/>
                </testsuite>
            </testsuites>
            """;

        var path = WriteTestFile("report.xml", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("azcore.TestRetryPolicy"));
        Assert.That(result.Items[0].StackTrace, Does.Contain("context deadline exceeded"));
    }

    [Test]
    public async Task ParsesErrorElements()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuite name="ErrorTests" tests="1" errors="1">
                <testcase classname="pkg.ErrorTests" name="testCrash" time="0.1">
                    <error message="NullPointerException" type="java.lang.NullPointerException">
            java.lang.NullPointerException
                at pkg.ErrorTests.testCrash(ErrorTests.java:10)
                    </error>
                </testcase>
            </testsuite>
            """;

        var path = WriteTestFile("TEST-errors.xml", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("pkg.ErrorTests.testCrash"));
        Assert.That(result.Items[0].ErrorMessage, Is.EqualTo("NullPointerException"));
        Assert.That(result.Items[0].Outcome, Is.EqualTo("java.lang.NullPointerException"));
    }

    [Test]
    public async Task GetFailedTestCases_FiltersByTitle()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuites>
                <testsuite name="tests" tests="2" failures="2">
                    <testcase classname="tests.auth" name="test_login" time="1.0">
                        <failure message="failed">stack</failure>
                    </testcase>
                    <testcase classname="tests.widget" name="test_create" time="1.0">
                        <failure message="failed">stack</failure>
                    </testcase>
                </testsuite>
            </testsuites>
            """;

        var path = WriteTestFile("results.xml", xml);
        var result = await _helper.GetFailedTestCases(path, "widget", CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].TestCaseTitle, Is.EqualTo("tests.widget.test_create"));
    }

    [Test]
    public void Throws_WhenFileNotFound()
    {
        Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _helper.GetFailedTestResults("/nonexistent/file.xml", CancellationToken.None));
    }

    [Test]
    public async Task ReturnsEmpty_WhenNoFailures()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuite name="AllPass" tests="2" failures="0">
                <testcase classname="pkg.Tests" name="test1" time="0.5"/>
                <testcase classname="pkg.Tests" name="test2" time="0.5"/>
            </testsuite>
            """;

        var path = WriteTestFile("pass.xml", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);
        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    public async Task ParsesMultipleTestSuites()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuites name="vitest tests" tests="6" failures="2" errors="0">
                <testsuite name="test/compute.spec.ts" tests="3" failures="1">
                    <testcase classname="test/compute.spec.ts" name="availabilitySets list test" time="1.8">
                        <failure message="Unable to find a record" type="RecordingError">Error: Unable to find a record</failure>
                    </testcase>
                    <testcase classname="test/compute.spec.ts" name="virtualMachines list test" time="0.5"/>
                    <testcase classname="test/compute.spec.ts" name="disks create test" time="0.6"/>
                </testsuite>
                <testsuite name="test/storage.spec.ts" tests="3" failures="1">
                    <testcase classname="test/storage.spec.ts" name="blobs upload test" time="1.2">
                        <failure message="Timeout exceeded" type="TimeoutError">Error: Timeout exceeded 5000ms</failure>
                    </testcase>
                    <testcase classname="test/storage.spec.ts" name="blobs download test" time="0.8"/>
                    <testcase classname="test/storage.spec.ts" name="blobs list test" time="0.4"/>
                </testsuite>
            </testsuites>
            """;

        var path = WriteTestFile("test-results.xml", xml);
        var result = await _helper.GetFailedTestResults(path, CancellationToken.None);

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items[0].TestCaseTitle, Does.Contain("availabilitySets list test"));
        Assert.That(result.Items[1].TestCaseTitle, Does.Contain("blobs upload test"));
        Assert.That(result.Items[1].ErrorMessage, Does.Contain("Timeout exceeded"));
    }

    [Test]
    public async Task CanParse_JUnitTestsuites()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuites><testsuite name="test"><testcase name="t1"/></testsuite></testsuites>
            """;
        var path = WriteTestFile("results.xml", xml);
        Assert.That(await _helper.CanParseAsync(path), Is.True);
    }

    [Test]
    public async Task CanParse_JUnitSingleTestsuite()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuite name="test"><testcase name="t1"/></testsuite>
            """;
        var path = WriteTestFile("results.xml", xml);
        Assert.That(await _helper.CanParseAsync(path), Is.True);
    }

    [Test]
    public async Task CanParse_ReturnsFalse_ForTrxContent()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                <Results><UnitTestResult testId="1" testName="Test1" outcome="Passed"/></Results>
            </TestRun>
            """;
        var path = WriteTestFile("results.xml", xml);
        Assert.That(await _helper.CanParseAsync(path), Is.False);
    }

    [Test]
    public async Task GetFailedTestCaseData_ReturnsSingleTest()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuite name="com.azure.test.MyTests" tests="2" failures="1" errors="0" time="1.0">
                <testcase name="failingTest" classname="com.azure.test.MyTests" time="0.5">
                    <failure message="expected true but was false" type="AssertionError">
            AssertionError: expected true but was false
                at com.azure.test.MyTests.failingTest(MyTests.java:10)
                    </failure>
                </testcase>
                <testcase name="passingTest" classname="com.azure.test.MyTests" time="0.3"/>
            </testsuite>
            """;
        var path = WriteTestFile("results.xml", xml);
        var result = await _helper.GetFailedTestCaseData(path, "com.azure.test.MyTests.failingTest", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.TestCaseTitle, Is.EqualTo("com.azure.test.MyTests.failingTest"));
        Assert.That(result.ErrorMessage, Does.Contain("expected true but was false"));
    }

    [Test]
    public async Task GetFailedTestCaseData_TitleNotFound_ReturnsError()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuite name="com.azure.test.MyTests" tests="1" failures="1" errors="0" time="0.5">
                <testcase name="failingTest" classname="com.azure.test.MyTests" time="0.5">
                    <failure message="oops" type="AssertionError">AssertionError: oops</failure>
                </testcase>
            </testsuite>
            """;
        var path = WriteTestFile("results.xml", xml);
        var result = await _helper.GetFailedTestCaseData(path, "NonExistentTest", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ResponseError, Does.Contain("No failed test run found"));
        Assert.That(result.ResponseError, Does.Contain("NonExistentTest"));
    }
}
