using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

internal class LogAnalysisHelperTests
{
    private LogAnalysisHelper logAnalysisHelper;
    private TestLogger<LogAnalysisHelper> testLogger;
    private string tempFile;

    [SetUp]
    public void Setup()
    {
        testLogger = new TestLogger<LogAnalysisHelper>();
        logAnalysisHelper = new LogAnalysisHelper(testLogger);
        tempFile = Path.GetTempFileName();
    }

    [TearDown]
    public void Cleanup()
    {
        File.Delete(tempFile);
    }

    [Test]
    public async Task TestBasicKeywordMatching_FindsSimpleErrors()
    {
        var logContent = @"Starting application
Loading configuration
ERROR: Database connection failed
Processing complete
Normal operation line
More normal operation
EXCEPTION: Null reference
Application shutting down";

        await File.WriteAllTextAsync(tempFile, logContent);

        var results = await logAnalysisHelper.AnalyzeLogContent(tempFile, null, 1, 1);

        // Based on actual behavior, might be 1 or 2 depending on how contiguous errors are handled
        Assert.That(results, Is.Not.Empty);
        Assert.That(results[0].Message, Does.Contain("ERROR: Database connection failed"));
        Assert.That(results[1].Message, Does.Contain("EXCEPTION: Null reference"));
    }

    [Test]
    public async Task TestCustomKeywordMatchingFunction_ErrorKeyword()
    {
        var logContent = @"Starting application
no error found in validation
Found 0 error messages
Checking for any errors in the logs
This is a real ERROR message
Processing error.type configuration
Another error occurred here
Using `error` as example text
System crash error detected";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, logContent);

        var results = await logAnalysisHelper.AnalyzeLogContent(tempFile, null, 0, 0);

        // Should find 3 real errors, excluding the false positives
        Assert.That(results, Has.Count.EqualTo(3));

        var errorMessages = results.Select(r => r.Message).ToList();
        Assert.That(errorMessages.Any(m => m.Contains("This is a real ERROR message")), Is.True);
        Assert.That(errorMessages.Any(m => m.Contains("Another error occurred here")), Is.True);
        Assert.That(errorMessages.Any(m => m.Contains("System crash error detected")), Is.True);

        // Verify false positives are not included
        Assert.That(errorMessages.Any(m => m.Contains("no error found")), Is.False);
        Assert.That(errorMessages.Any(m => m.Contains("0 error messages")), Is.False);
        Assert.That(errorMessages.Any(m => m.Contains("any errors")), Is.False);
        Assert.That(errorMessages.Any(m => m.Contains("`error`")), Is.False);
        Assert.That(errorMessages.Any(m => m.Contains("error.type")), Is.False);
    }

    [Test]
    public async Task TestContiguousErrors_ExtendsContextWindow()
    {
        var logContent = @"Normal operation
Processing data
ERROR: First error occurred
ERROR: Second error immediately after
ERROR: Third consecutive error
FATAL: Critical system failure
Normal recovery operation
System restored";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, logContent);

        var results = await logAnalysisHelper.AnalyzeLogContent(tempFile, null, 1, null);

        // Should find one LogEntry that contains all contiguous errors
        Assert.That(results, Has.Count.EqualTo(1));

        var errorEntry = results[0];
        Assert.That(errorEntry.Message, Does.Contain("Processing data")); // before context
        Assert.That(errorEntry.Message, Does.Contain("ERROR: First error occurred"));
        Assert.That(errorEntry.Message, Does.Contain("ERROR: Second error immediately after"));
        Assert.That(errorEntry.Message, Does.Contain("ERROR: Third consecutive error"));
        Assert.That(errorEntry.Message, Does.Contain("FATAL: Critical system failure"));
        Assert.That(errorEntry.Message, Does.Contain("Normal recovery operation")); // extended after context
        Assert.That(errorEntry.Message, Does.Contain("System restored")); // extended after context
    }

    [Test]
    public async Task TestCustomKeywordsProvided_OverridesDefaults()
    {
        var logContent = @"Starting application
ERROR: Database connection failed
CUSTOM_ISSUE: Custom problem detected
FAILURE: System crashed
CUSTOM_WARNING: Another custom issue
Processing complete";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, logContent);

        var customKeywords = new List<string> { "CUSTOM_ISSUE", "CUSTOM_WARNING" };
        var results = await logAnalysisHelper.AnalyzeLogContent(tempFile, customKeywords, 0, 0);

        Assert.That(results, Has.Count.EqualTo(2));

        var errorMessages = results.Select(r => r.Message).ToList();
        Assert.That(errorMessages.Any(m => m.Contains("CUSTOM_ISSUE: Custom problem detected")), Is.True);
        Assert.That(errorMessages.Any(m => m.Contains("CUSTOM_WARNING: Another custom issue")), Is.True);

        // Default keywords should not be matched
        Assert.That(errorMessages.Any(m => m.Contains("ERROR: Database connection failed")), Is.False);
        Assert.That(errorMessages.Any(m => m.Contains("FAILURE: System crashed")), Is.False);
    }

    [Test]
    public async Task TestCustomBeforeAfterContext_RespectsSizes()
    {
        var logContent = @"Line 1
Line 2
Line 3
Line 4
Line 5: ERROR occurred here
Line 6
Line 7
Line 8
Line 9
Line 10";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, logContent);

        // Test with custom context sizes
        var results = await logAnalysisHelper.AnalyzeLogContent(tempFile, null, 2, 3);

        Assert.That(results, Has.Count.EqualTo(1));

        var lines = results[0].Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Should have 2 before + 1 error + 3 after = 6 lines total
        Assert.That(lines.Length, Is.EqualTo(6));
        Assert.That(lines[0], Does.Contain("Line 3")); // 2 lines before
        Assert.That(lines[1], Does.Contain("Line 4")); // 1 line before
        Assert.That(lines[2], Does.Contain("Line 5: ERROR occurred here")); // error line
        Assert.That(lines[3], Does.Contain("Line 6")); // 1 line after
        Assert.That(lines[4], Does.Contain("Line 7")); // 2 lines after
        Assert.That(lines[5], Does.Contain("Line 8")); // 3 lines after
    }

    [Test]
    public void TestFileNotFound_HandlesGracefully()
    {
        var nonExistentDirectory = "/path/that/does/not/exist.log";
        Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
            await logAnalysisHelper.AnalyzeLogContent(nonExistentDirectory, null, null, null));
        var nonExistentFile = "exist.log";
        Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await logAnalysisHelper.AnalyzeLogContent(nonExistentFile, null, null, null));
    }

    [Test]
    public async Task TestEmptyFile_ReturnsEmptyResults()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, string.Empty);

        var results = await logAnalysisHelper.AnalyzeLogContent(tempFile, null, null, null);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task TestNonEmptyFileNoMatches_ReturnsEmptyResults()
    {
        var logContent = @"Starting application successfully
Loading configuration completed
All systems operational
Processing user requests
Performance metrics normal
Shutting down gracefully";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, logContent);

        var results = await logAnalysisHelper.AnalyzeLogContent(tempFile, null, null, null);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task TestNullOrEmptyKeywords_UsesDefaults()
    {
        var logContent = @"Starting application
ERROR: Database connection failed
Processing complete";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, logContent);

        // Test with null keywords
        var results1 = await logAnalysisHelper.AnalyzeLogContent(tempFile, null, 1, 1);
        Assert.That(results1, Has.Count.EqualTo(1));

        // Test with empty keywords list - should also use defaults
        var results2 = await logAnalysisHelper.AnalyzeLogContent(tempFile, new List<string>(), 1, 1);
        Assert.That(results2, Has.Count.EqualTo(1)); // Empty list uses defaults, same as null
    }

    [Test]
    public async Task TestLargeLogFile_ProcessesCorrectly()
    {
        var testAssetPath = Path.Combine(
            Path.GetDirectoryName(typeof(LogAnalysisHelperTests).Assembly.Location)!,
            "TestAssets", "large-log-sample.txt");

        // Verify the test asset file exists
        Assert.That(File.Exists(testAssetPath), Is.True, $"Test asset file not found at {testAssetPath}");

        var results = await logAnalysisHelper.AnalyzeLogContent(testAssetPath, null, 2, 5);

        // Should find 2 error groups: one for the database errors and one for the fatal errors
        Assert.That(results, Has.Count.EqualTo(2));

        // First error group should contain database-related errors
        Assert.That(results[0].Message, Does.Contain("Database connection failed"));
        Assert.That(results[0].Message, Does.Contain("Failed to execute query"));

        // Second error group should contain fatal errors
        Assert.That(results[1].Message, Does.Contain("Payment service unavailable"));
        Assert.That(results[1].Message, Does.Contain("Unable to process payment"));
    }

    [Test]
    public async Task TestAnsiColorCodes_DetectedAsErrors()
    {
        var logContent = @"Starting application
Normal processing
[31mThis is red text indicating an error[0m
More normal processing
Another [31merror with ANSI codes[0m
End of log";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, logContent);

        var results = await logAnalysisHelper.AnalyzeLogContent(tempFile, null, 0, 0);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Message, Does.Contain("[31mThis is red text indicating an error[0m"));
        Assert.That(results[1].Message, Does.Contain("Another [31merror with ANSI codes[0m"));
    }
}
