// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    public class FileHelperTests
    {
    private TempDirectory _tempDir;
        private ILogger _logger;

        [SetUp]
        public void SetUp()
        {
            _tempDir = TempDirectory.Create("FileHelperTests");
            
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<FileHelperTests>();
        }

        [TearDown]
        public void TearDown()
        {
            _tempDir.Dispose();
        }

        #region LoadFilesAsync Tests

        [Test]
        public async Task LoadFilesAsync_WithSingleFile_ShouldReturnFileContent()
        {
            // Arrange
            var testFile = Path.Combine(_tempDir.DirectoryPath, "test.cs");
            var content = "using System;\npublic class Test { }";
            await File.WriteAllTextAsync(testFile, content);

            // Act
            var result = await FileHelper.LoadFilesAsync(
                filePaths: new[] { testFile },
                includeExtensions: new[] { ".cs" },
                excludeGlobPatterns: Array.Empty<string>(),
                relativeTo: _tempDir.DirectoryPath,
                totalBudget: 1000,
                perFileLimit: 500,
                priorityFunc: _ => 1);

            // Assert
            Assert.That(result, Does.Contain("test.cs"));
            Assert.That(result, Does.Contain(content));
            Assert.That(result, Does.Contain("<file path="));
        }

        [Test]
        public async Task LoadFilesAsync_WithDirectory_ShouldReturnFilteredFiles()
        {
            // Arrange
            var subDir = Path.Combine(_tempDir.DirectoryPath, "src");
            Directory.CreateDirectory(subDir);
            
            var csFile = Path.Combine(subDir, "code.cs");
            var jsFile = Path.Combine(subDir, "script.js");
            var txtFile = Path.Combine(subDir, "readme.txt");
            
            await File.WriteAllTextAsync(csFile, "// C# code");
            await File.WriteAllTextAsync(jsFile, "// JS code");
            await File.WriteAllTextAsync(txtFile, "Documentation");

            // Act
            var result = await FileHelper.LoadFilesAsync(
                filePaths: new[] { _tempDir.DirectoryPath },
                includeExtensions: new[] { ".cs", ".js" },
                excludeGlobPatterns: Array.Empty<string>(),
                relativeTo: _tempDir.DirectoryPath,
                totalBudget: 2000,
                perFileLimit: 1000,
                priorityFunc: _ => 1);

            // Assert
            Assert.That(result, Does.Contain("code.cs"));
            Assert.That(result, Does.Contain("script.js"));
            Assert.That(result, Does.Not.Contain("readme.txt"));
            Assert.That(result, Does.Contain("// C# code"));
            Assert.That(result, Does.Contain("// JS code"));
        }

        [Test]
        public async Task LoadFilesAsync_WithExcludePatterns_ShouldFilterOutMatchingFiles()
        {
            // Arrange
            var testDir = Path.Combine(_tempDir.DirectoryPath, "test");
            var srcDir = Path.Combine(_tempDir.DirectoryPath, "src");
            Directory.CreateDirectory(testDir);
            Directory.CreateDirectory(srcDir);
            
            var testFile = Path.Combine(testDir, "unit.cs");
            var srcFile = Path.Combine(srcDir, "main.cs");
            
            await File.WriteAllTextAsync(testFile, "// Test code");
            await File.WriteAllTextAsync(srcFile, "// Main code");

            // Act
            var result = await FileHelper.LoadFilesAsync(
                filePaths: new[] { _tempDir.DirectoryPath },
                includeExtensions: new[] { ".cs" },
                excludeGlobPatterns: new[] { "**/test/**" },
                relativeTo: _tempDir.DirectoryPath,
                totalBudget: 2000,
                perFileLimit: 1000,
                priorityFunc: _ => 1);

            // Assert
            Assert.That(result, Does.Not.Contain("unit.cs"), "Files in test directory should be excluded");
            Assert.That(result, Does.Contain("main.cs"), "Files in src directory should be included");
            Assert.That(result, Does.Contain("// Main code"));
        }

        [Test]
        public async Task LoadFilesAsync_WithBudgetConstraint_ShouldRespectTotalBudget()
        {
            // Arrange
            var file1 = Path.Combine(_tempDir.DirectoryPath, "file1.cs");
            var file2 = Path.Combine(_tempDir.DirectoryPath, "file2.cs");
            var longContent = new string('a', 500);
            
            await File.WriteAllTextAsync(file1, longContent);
            await File.WriteAllTextAsync(file2, longContent);

            // Act - Set budget that can only fit one file + overhead
            var result = await FileHelper.LoadFilesAsync(
                filePaths: new[] { _tempDir.DirectoryPath },
                includeExtensions: new[] { ".cs" },
                excludeGlobPatterns: Array.Empty<string>(),
                relativeTo: _tempDir.DirectoryPath,
                totalBudget: 600, // Only enough for one file + headers
                perFileLimit: 500,
                priorityFunc: _ => 1);

            // Assert
            var fileMatches = System.Text.RegularExpressions.Regex.Matches(result, @"<file path=");
            Assert.That(fileMatches.Count, Is.EqualTo(1), "Should only include one file due to budget constraint");
            Assert.That(result, Does.Contain("additional files omitted"));
        }

        [Test]
        public async Task LoadFilesAsync_WithPerFileLimit_ShouldTruncateFiles()
        {
            // Arrange
            var testFile = Path.Combine(_tempDir.DirectoryPath, "large.cs");
            var longContent = new string('x', 1000);
            await File.WriteAllTextAsync(testFile, longContent);

            // Act
            var result = await FileHelper.LoadFilesAsync(
                filePaths: new[] { testFile },
                includeExtensions: new[] { ".cs" },
                excludeGlobPatterns: Array.Empty<string>(),
                relativeTo: _tempDir.DirectoryPath,
                totalBudget: 2000,
                perFileLimit: 500, // Smaller than file size
                priorityFunc: _ => 1);

            // Assert
            Assert.That(result, Does.Contain("large.cs"));
            Assert.That(result, Does.Contain("truncated"));
            // The actual content should be less than the original
            var contentStart = result.IndexOf(new string('x', 100));
            Assert.That(contentStart, Is.GreaterThan(-1));
        }

        [Test]
        public async Task LoadFilesAsync_WithSourceInputs_ShouldApplyPerInputFiltering()
        {
            // Arrange
            var dir1 = Path.Combine(_tempDir.DirectoryPath, "dir1");
            var dir2 = Path.Combine(_tempDir.DirectoryPath, "dir2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);
            
            var csFile = Path.Combine(dir1, "code.cs");
            var jsFile = Path.Combine(dir2, "script.js");
            var testFile = Path.Combine(dir2, "test.js");
            
            await File.WriteAllTextAsync(csFile, "// C# code");
            await File.WriteAllTextAsync(jsFile, "// JS code");
            await File.WriteAllTextAsync(testFile, "// Test JS");

            var inputs = new[]
            {
                new FileHelper.SourceInput(dir1, IncludeExtensions: new[] { ".cs" }),
                new FileHelper.SourceInput(dir2, IncludeExtensions: new[] { ".js" }, ExcludeGlobPatterns: new[] { "**/test.js" })
            };

            // Act
            var result = await FileHelper.LoadFilesAsync(
                inputs: inputs,
                relativeTo: _tempDir.DirectoryPath,
                totalBudget: 2000,
                perFileLimit: 1000,
                priorityFunc: _ => 1);

            // Assert
            Assert.That(result, Does.Contain("code.cs"));
            Assert.That(result, Does.Contain("script.js"));
            Assert.That(result, Does.Not.Contain("test.js"), "test.js should be excluded by the glob pattern");
        }

        [Test]
        public async Task LoadFilesAsync_WithEmptyDirectory_ShouldReturnEmptyString()
        {
            // Arrange
            var emptyDir = Path.Combine(_tempDir.DirectoryPath, "empty");
            Directory.CreateDirectory(emptyDir);

            // Act
            var result = await FileHelper.LoadFilesAsync(
                filePaths: new[] { emptyDir },
                includeExtensions: new[] { ".cs" },
                excludeGlobPatterns: Array.Empty<string>(),
                relativeTo: _tempDir.DirectoryPath,
                totalBudget: 1000,
                perFileLimit: 500,
                priorityFunc: _ => 1);

            // Assert
            Assert.That(result, Is.Empty);
        }

        #endregion

        #region DiscoverFiles Tests

        [Test]
        public void DiscoverFiles_WithMixedPaths_ShouldReturnCorrectMetadata()
        {
            // Arrange
            var subDir = Path.Combine(_tempDir.DirectoryPath, "src");
            Directory.CreateDirectory(subDir);
            
            var file1 = Path.Combine(_tempDir.DirectoryPath, "root.cs");
            var file2 = Path.Combine(subDir, "nested.cs");
            var file3 = Path.Combine(subDir, "script.js");
            
            File.WriteAllText(file1, "// Root");
            File.WriteAllText(file2, "// Nested C#");
            File.WriteAllText(file3, "// Script");

            // Act
            var files = FileHelper.DiscoverFiles(
                filePaths: new[] { _tempDir.DirectoryPath },
                includeExtensions: new[] { ".cs" },
                excludeGlobPatterns: Array.Empty<string>(),
                relativeTo: _tempDir.DirectoryPath,
                priorityFunc: f => f.FileSize);

            // Assert
            Assert.That(files.Count, Is.EqualTo(2));
            Assert.That(files.Any(f => f.RelativePath.EndsWith("root.cs")), Is.True);
            Assert.That(files.Any(f => f.RelativePath.EndsWith("nested.cs")), Is.True);
            Assert.That(files.Any(f => f.RelativePath.EndsWith("script.js")), Is.False);
            
            foreach (var file in files)
            {
                Assert.That(file.FileSize, Is.GreaterThan(0));
                Assert.That(file.Priority, Is.EqualTo(file.FileSize));
            }
        }

        [Test]
        public void DiscoverFiles_WithPriorityFunction_ShouldSortCorrectly()
        {
            // Arrange
            var smallFile = Path.Combine(_tempDir.DirectoryPath, "small.cs");
            var largeFile = Path.Combine(_tempDir.DirectoryPath, "large.cs");
            
            File.WriteAllText(smallFile, "small");
            File.WriteAllText(largeFile, new string('x', 100));

            // Act - Priority by size (ascending)
            var files = FileHelper.DiscoverFiles(
                filePaths: new[] { _tempDir.DirectoryPath },
                includeExtensions: new[] { ".cs" },
                excludeGlobPatterns: Array.Empty<string>(),
                relativeTo: _tempDir.DirectoryPath,
                priorityFunc: f => f.FileSize);

            // Assert
            Assert.That(files.Count, Is.EqualTo(2));
            Assert.That(files[0].RelativePath, Does.EndWith("small.cs"));
            Assert.That(files[1].RelativePath, Does.EndWith("large.cs"));
            Assert.That(files[0].Priority, Is.LessThan(files[1].Priority));
        }

        #endregion

        #region CreateFileLoadingPlan Tests

        [Test]
        public void CreateFileLoadingPlan_WithBudgetConstraint_ShouldCreateValidPlan()
        {
            // Arrange
            var files = new List<FileHelper.FileMetadata>
            {
                new("file1.cs", "file1.cs", 300, 1),
                new("file2.cs", "file2.cs", 400, 2),
                new("file3.cs", "file3.cs", 500, 3)
            };

            // Act
            var plan = FileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 800, perFileLimit: 400);

            // Assert
            Assert.That(plan.TotalFilesFound, Is.EqualTo(3));
            Assert.That(plan.TotalFilesIncluded, Is.LessThanOrEqualTo(3));
            Assert.That(plan.BudgetUsed, Is.LessThanOrEqualTo(800));
            Assert.That(plan.Items.All(i => i.ContentToLoad <= 400), Is.True);
            
            foreach (var item in plan.Items)
            {
                Assert.That(item.ContentToLoad, Is.LessThanOrEqualTo(item.FileSize));
                Assert.That(item.IsTruncated, Is.EqualTo(item.ContentToLoad < item.FileSize));
            }
        }

        [Test]
        public void CreateFileLoadingPlan_WithZeroBudget_ShouldReturnEmptyPlan()
        {
            // Arrange
            var files = new List<FileHelper.FileMetadata>
            {
                new("file1.cs", "file1.cs", 100, 1)
            };

            // Act
            var plan = FileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 0, perFileLimit: 100);

            // Assert
            Assert.That(plan.Items.Count, Is.EqualTo(0));
            Assert.That(plan.TotalFilesFound, Is.EqualTo(1));
            Assert.That(plan.TotalFilesIncluded, Is.EqualTo(0));
        }

        #endregion

        #region ExecuteFileLoadingPlan Tests

        [Test]
        public async Task ExecuteFileLoadingPlan_WithValidPlan_ShouldGenerateCorrectOutput()
        {
            // Arrange
            var testFile = Path.Combine(_tempDir.DirectoryPath, "test.cs");
            var content = "using System;\nclass Test { }";
            await File.WriteAllTextAsync(testFile, content);

            var plan = new FileHelper.FileLoadingPlan(
                Items: new List<FileHelper.FileLoadingItem>
                {
                    new(testFile, "test.cs", content.Length, content.Length, content.Length / 4, false)
                },
                TotalFilesFound: 1,
                TotalFilesIncluded: 1,
                TotalEstimatedTokens: content.Length / 4,
                BudgetUsed: content.Length + 50,
                TotalBudget: 1000);

            // Act
            var result = await FileHelper.ExecuteFileLoadingPlanAsync(plan, _logger);

            // Assert
            Assert.That(result, Does.Contain("<file path=\"test.cs\""));
            Assert.That(result, Does.Contain("using System;"));
            Assert.That(result, Does.Contain("class Test"));
            Assert.That(result, Does.Contain("</file>"));
        }

        [Test]
        public async Task ExecuteFileLoadingPlan_WithTruncatedFile_ShouldShowTruncation()
        {
            // Arrange
            var testFile = Path.Combine(_tempDir.DirectoryPath, "large.cs");
            var content = new string('x', 1000);
            await File.WriteAllTextAsync(testFile, content);

            var plan = new FileHelper.FileLoadingPlan(
                Items: new List<FileHelper.FileLoadingItem>
                {
                    new(testFile, "large.cs", content.Length, 500, 125, true)
                },
                TotalFilesFound: 1,
                TotalFilesIncluded: 1,
                TotalEstimatedTokens: 125,
                BudgetUsed: 550,
                TotalBudget: 1000);

            // Act
            var result = await FileHelper.ExecuteFileLoadingPlanAsync(plan, _logger);

            // Assert
            Assert.That(result, Does.Contain("large.cs"));
            Assert.That(result, Does.Contain("truncated"));
            // Should contain some x's but not all 1000
            Assert.That(result, Does.Contain("xxx"));
            var xMatches = System.Text.RegularExpressions.Regex.Matches(result, "x");
            Assert.That(xMatches.Count, Is.LessThan(1000));
        }

        [Test]
        public async Task ExecuteFileLoadingPlan_WithNonExistentFile_ShouldHandleGracefully()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_tempDir.DirectoryPath, "missing.cs");

            var plan = new FileHelper.FileLoadingPlan(
                Items: new List<FileHelper.FileLoadingItem>
                {
                    new(nonExistentFile, "missing.cs", 100, 100, 25, false)
                },
                TotalFilesFound: 1,
                TotalFilesIncluded: 1,
                TotalEstimatedTokens: 25,
                BudgetUsed: 150,
                TotalBudget: 1000);

            // Act
            var result = await FileHelper.ExecuteFileLoadingPlanAsync(plan, _logger);

            // Assert
            Assert.That(result, Does.Contain("missing.cs"));
            Assert.That(result, Does.Contain("error="));
        }

        #endregion

    }
}