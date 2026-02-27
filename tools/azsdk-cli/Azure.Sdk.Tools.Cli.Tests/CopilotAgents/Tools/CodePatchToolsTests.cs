// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents.Tools;

internal class CodePatchToolsTests
{
    private TempDirectory? temp;
    private string baseDir => temp!.DirectoryPath;

    [SetUp]
    public void Setup()
    {
        temp = TempDirectory.Create("codepatch-tests");
    }

    [TearDown]
    public void TearDown()
    {
        temp?.Dispose();
    }

    private string CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(baseDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    #region File Resolution Tests

    [Test]
    public async Task ApplyPatch_PathTraversal_ReturnsError()
    {
        var result = await CodePatchTools.ApplyPatchAsync(baseDir, "../../../etc/passwd", 1, 1, "old", "new", CancellationToken.None);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("outside the customization directory"));
    }

    #endregion

    #region Exact Match Replacement Tests

    [Test]
    public async Task ApplyPatch_ExactMatch_SingleLine_ReplacesCorrectly()
    {
        CreateFile("Test.java", "public class Test {\n    int x = 1;\n    int y = 2;\n}\n");

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Test.java", 2, 2, "int x = 1;", "int x = 42;", CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var content = File.ReadAllText(Path.Combine(baseDir, "Test.java"));
        Assert.That(content, Does.Contain("int x = 42;"));
        Assert.That(content, Does.Contain("int y = 2;"));  // Surrounding lines unchanged
    }

    [Test]
    public async Task ApplyPatch_ExactMatch_MultiLine_ReplacesCorrectly()
    {
        var original = "line 1\nline 2\nline 3\nline 4\nline 5\n";
        CreateFile("Multi.java", original);

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Multi.java", 2, 4, "line 2\nline 3\nline 4", "replaced 2\nreplaced 3\nreplaced 4",
            CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var lines = File.ReadAllLines(Path.Combine(baseDir, "Multi.java"));
        Assert.That(lines[0], Is.EqualTo("line 1"));
        Assert.That(lines[1], Is.EqualTo("replaced 2"));
        Assert.That(lines[2], Is.EqualTo("replaced 3"));
        Assert.That(lines[3], Is.EqualTo("replaced 4"));
        Assert.That(lines[4], Is.EqualTo("line 5"));
    }

    [Test]
    public async Task ApplyPatch_ExactMatch_DeletionWithEmptyNewText()
    {
        CreateFile("Del.java", "keep this\nremove me\nkeep this too\n");

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Del.java", 2, 2, "remove me", "", CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var content = File.ReadAllText(Path.Combine(baseDir, "Del.java"));
        Assert.That(content, Does.Contain("keep this"));
        Assert.That(content, Does.Not.Contain("remove me"));
    }

    [Test]
    public async Task ApplyPatch_MultipleOccurrences_ReturnsError()
    {
        CreateFile("Dup.java", "dup\ndup\ndup\n");

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Dup.java", 1, 3, "dup", "unique", CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("multiple times"));
    }

    [Test]
    public async Task ApplyPatch_PartialLineMatch_ReplacesOnlyMatchedPart()
    {
        CreateFile("Partial.java", "int x = oldValue + bonus;\n");

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Partial.java", 1, 1, "oldValue", "newValue", CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var content = File.ReadAllText(Path.Combine(baseDir, "Partial.java"));
        Assert.That(content, Does.Contain("int x = newValue + bonus;"));
    }

    #endregion

    #region Fuzzy Whitespace Matching Tests

    [Test]
    public async Task ApplyPatch_FuzzyMatch_ExtraSpaces_ReplacesCorrectly()
    {
        // File has extra spaces between tokens
        CreateFile("Fuzzy.java", "public   void   foo() {\n    return;\n}\n");

        // LLM provides normalized whitespace
        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Fuzzy.java", 1, 1, "public void foo() {", "public void bar() {", CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var content = File.ReadAllText(Path.Combine(baseDir, "Fuzzy.java"));
        Assert.That(content, Does.Contain("bar"));
        Assert.That(content, Does.Contain("return;"));  // Rest of file unchanged
    }

    [Test]
    public async Task ApplyPatch_FuzzyMatch_PreservesCorrectSpan()
    {
        // Key bug scenario: whitespace differs so the matched span length differs
        // from cleanOldText.Length. The fix ensures we replace the actual matched span.
        CreateFile("SpanTest.java",
            "    public   String   getValue() {\n" +
            "        return value;\n" +
            "    }\n");

        // LLM provides single-spaced version
        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "SpanTest.java", 1, 1,
            "public String getValue() {",
            "public String getNewValue() {",
            CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var lines = File.ReadAllLines(Path.Combine(baseDir, "SpanTest.java"));
        Assert.That(lines[0], Does.Contain("getNewValue"));
        Assert.That(lines[1], Does.Contain("return value;"));  // Next line must be untouched
    }

    [Test]
    public async Task ApplyPatch_FuzzyMatch_TabsVsSpaces()
    {
        // File uses tabs, LLM provides spaces
        CreateFile("Tabs.java", "public\tvoid\tmethod() {\n}\n");

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Tabs.java", 1, 1,
            "public void method() {",
            "public void newMethod() {",
            CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var content = File.ReadAllText(Path.Combine(baseDir, "Tabs.java"));
        Assert.That(content, Does.Contain("newMethod"));
    }

    [Test]
    public async Task ApplyPatch_FuzzyMatch_NewlinesCountAsWhitespace()
    {
        // OldText from LLM has content on one line, but file has it across multiple lines
        CreateFile("Newlines.java", "foo(\n    arg1,\n    arg2\n);\nkeep\n");

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Newlines.java", 1, 4,
            "foo( arg1, arg2 );",
            "bar(arg1, arg2);",
            CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var content = File.ReadAllText(Path.Combine(baseDir, "Newlines.java"));
        Assert.That(content, Does.Contain("bar"));
        Assert.That(content, Does.Contain("keep"));
    }

    [Test]
    public async Task ApplyPatch_FuzzyMatch_MultipleOccurrences_ReturnsError()
    {
        // Both occurrences only match via fuzzy (neither is exact), so ambiguity check kicks in
        CreateFile("FuzzyDup.java", "x  =  1;\nkeep\nx  =  1;\n");

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "FuzzyDup.java", 1, 3, "x = 1;", "x = 2;", CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("multiple times"));
    }

    #endregion

    #region Line Number Prefix Stripping Tests

    [Test]
    public async Task ApplyPatch_StripsLineNumberPrefixes_FromOldText()
    {
        CreateFile("Numbered.java", "alpha\nbeta\ngamma\n");

        // LLM copies text from ReadFile output which includes line numbers
        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "Numbered.java", 2, 2, "2: beta", "delta", CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var content = File.ReadAllText(Path.Combine(baseDir, "Numbered.java"));
        Assert.That(content, Does.Contain("delta"));
        Assert.That(content, Does.Not.Contain("beta"));
    }

    [Test]
    public async Task ApplyPatch_StripsLineNumberPrefixes_FromNewText()
    {
        CreateFile("NumNew.java", "old content\n");

        var result = await CodePatchTools.ApplyPatchAsync(
            baseDir, "NumNew.java", 1, 1, "old content", "1: new content", CancellationToken.None);

        Assert.That(result.Success, Is.True);

        var content = File.ReadAllText(Path.Combine(baseDir, "NumNew.java"));
        Assert.That(content, Does.Contain("new content"));
        Assert.That(content, Does.Not.StartWith("1:"));
    }

    #endregion
}
