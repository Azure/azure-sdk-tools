// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class TestResultParserResolverTests
{
    private TestResultParserResolver _resolver;
    private TempDirectory _tempDir;

    [SetUp]
    public void SetUp()
    {
        var trxParser = new TrxTestHelper();
        var junitParser = new JUnitTestHelper();
        _resolver = new TestResultParserResolver([trxParser, junitParser]);
        _tempDir = TempDirectory.Create("TestResultParserResolverTests");
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
    public async Task Resolve_TrxFile_ReturnsTrxParser()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                <Results><UnitTestResult testId="1" testName="Test1" outcome="Failed"/></Results>
            </TestRun>
            """;
        var path = WriteTestFile("results.trx", xml);
        var parser = await _resolver.ResolveAsync(path);

        Assert.That(parser, Is.Not.Null);
        Assert.That(parser!.FormatName, Is.EqualTo("TRX"));
    }

    [Test]
    public async Task Resolve_JUnitFile_ReturnsJUnitParser()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuites><testsuite name="tests" tests="1"><testcase name="t1"/></testsuite></testsuites>
            """;
        var path = WriteTestFile("results.xml", xml);
        var parser = await _resolver.ResolveAsync(path);

        Assert.That(parser, Is.Not.Null);
        Assert.That(parser!.FormatName, Is.EqualTo("JUnit XML"));
    }

    [Test]
    public async Task Resolve_SingleTestsuite_ReturnsJUnitParser()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <testsuite name="tests" tests="1"><testcase name="t1"/></testsuite>
            """;
        var path = WriteTestFile("results.xml", xml);
        var parser = await _resolver.ResolveAsync(path);

        Assert.That(parser, Is.Not.Null);
        Assert.That(parser!.FormatName, Is.EqualTo("JUnit XML"));
    }

    [Test]
    public async Task Resolve_TrxContentWithXmlExtension_ReturnsTrxParser()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                <Results><UnitTestResult testId="1" testName="Test1" outcome="Passed"/></Results>
            </TestRun>
            """;
        var path = WriteTestFile("results.xml", xml);
        var parser = await _resolver.ResolveAsync(path);

        Assert.That(parser, Is.Not.Null);
        Assert.That(parser!.FormatName, Is.EqualTo("TRX"));
    }

    [Test]
    public void Resolve_UnrecognizedFormat_Throws()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <html><body>Not a test result</body></html>
            """;
        var path = WriteTestFile("results.xml", xml);

        Assert.ThrowsAsync<InvalidOperationException>(() => _resolver.ResolveAsync(path));
    }

    [Test]
    public void Resolve_NonXmlFile_Throws()
    {
        var path = WriteTestFile("results.txt", "This is not XML");

        Assert.ThrowsAsync<InvalidOperationException>(() => _resolver.ResolveAsync(path));
    }

    [Test]
    public void Resolve_NullPath_ThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(() => _resolver.ResolveAsync(null!));
    }

    [Test]
    public void Resolve_EmptyPath_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(() => _resolver.ResolveAsync(""));
    }

    [Test]
    public void Resolve_MissingFile_ThrowsFileNotFound()
    {
        Assert.ThrowsAsync<FileNotFoundException>(() => _resolver.ResolveAsync("/nonexistent/file.xml"));
    }

    [Test]
    public void SupportedFormats_ListsAllRegisteredParsers()
    {
        var formats = _resolver.SupportedFormats;

        Assert.That(formats, Has.Count.EqualTo(2));
        Assert.That(formats, Does.Contain("TRX"));
        Assert.That(formats, Does.Contain("JUnit XML"));
    }
}
