// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    [TestFixture]
    public partial class FileHelperTests
    {
    private TempDirectory _tempDir;
        private ILogger<FileHelper> _logger;
        private FileHelper _fileHelper;

        [SetUp]
        public void SetUp()
        {
            _tempDir = TempDirectory.Create("FileHelperTests");

            _logger = new TestLogger<FileHelper>();
            _fileHelper = new FileHelper(_logger);
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
            var result = await _fileHelper.LoadFilesAsync(
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
            var result = await _fileHelper.LoadFilesAsync(
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
            var result = await _fileHelper.LoadFilesAsync(
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
            var result = await _fileHelper.LoadFilesAsync(
                filePaths: new[] { _tempDir.DirectoryPath },
                includeExtensions: new[] { ".cs" },
                excludeGlobPatterns: Array.Empty<string>(),
                relativeTo: _tempDir.DirectoryPath,
                totalBudget: 600, // Only enough for one file + headers
                perFileLimit: 500,
                priorityFunc: _ => 1);

            // Assert
            var fileMatches = FilePathElementRegex().Matches(result);
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
            var result = await _fileHelper.LoadFilesAsync(
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
                new SourceInput(dir1, IncludeExtensions: new[] { ".cs" }),
                new SourceInput(dir2, IncludeExtensions: new[] { ".js" }, ExcludeGlobPatterns: new[] { "**/test.js" })
            };

            // Act
            var result = await _fileHelper.LoadFilesAsync(
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
            var result = await _fileHelper.LoadFilesAsync(
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
            var files = _fileHelper.DiscoverFiles(
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
            var files = _fileHelper.DiscoverFiles(
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
            var files = new List<FileMetadata>
            {
                new("file1.cs", "file1.cs", 300, 1),
                new("file2.cs", "file2.cs", 400, 2),
                new("file3.cs", "file3.cs", 500, 3)
            };

            // Act
            var plan = _fileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 800, perFileLimit: 400);

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
            var files = new List<FileMetadata>
            {
                new("file1.cs", "file1.cs", 100, 1)
            };

            // Act
            var plan = _fileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 0, perFileLimit: 100);

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
            var result = await _fileHelper.ExecuteFileLoadingPlanAsync(plan);

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
            var result = await _fileHelper.ExecuteFileLoadingPlanAsync(plan);

            // Assert
            Assert.That(result, Does.Contain("large.cs"));
            Assert.That(result, Does.Contain("truncated"));
            // Should contain some x's but not all 1000
            Assert.That(result, Does.Contain("xxx"));
            var xMatches = SingleCharacterXRegex().Matches(result);
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
            var result = await _fileHelper.ExecuteFileLoadingPlanAsync(plan);

            // Assert
            Assert.That(result, Does.Contain("missing.cs"));
            Assert.That(result, Does.Contain("error="));
        }

        #endregion

        #region Empty File Handling Tests

        [Test]
        public void CreateLoadingPlanFromMetadata_WithEmptyFiles_ShouldSkipAndContinueProcessing()
        {
            // Arrange - Mix of empty and non-empty files
            var files = new List<FileMetadata>
            {
                new("file1.cs", "file1.cs", 100, 1),     // Normal file
                new("empty1.cs", "empty1.cs", 0, 2),     // Empty file
                new("file2.cs", "file2.cs", 200, 3),     // Normal file
                new("empty2.cs", "empty2.cs", 0, 4),     // Another empty file
                new("file3.cs", "file3.cs", 150, 5)      // Normal file
            };

            // Act
            var plan = _fileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 1000, perFileLimit: 500);

            // Assert
            Assert.That(plan.TotalFilesFound, Is.EqualTo(5), "Should count all files including empty ones");
            Assert.That(plan.TotalFilesIncluded, Is.EqualTo(3), "Should only include non-empty files");
            Assert.That(plan.Items.Count, Is.EqualTo(3), "Should only have loading items for non-empty files");
            
            // Verify only non-empty files are included
            var includedFiles = plan.Items.Select(i => i.RelativePath).ToList();
            Assert.That(includedFiles, Does.Contain("file1.cs"));
            Assert.That(includedFiles, Does.Contain("file2.cs"));
            Assert.That(includedFiles, Does.Contain("file3.cs"));
            Assert.That(includedFiles, Does.Not.Contain("empty1.cs"));
            Assert.That(includedFiles, Does.Not.Contain("empty2.cs"));
            
            // Verify budget calculation excludes empty files
            var expectedBudget = 100 + 50 + 200 + 50 + 150 + 50; // file sizes + header overhead each
            Assert.That(plan.BudgetUsed, Is.EqualTo(expectedBudget));
        }

        [Test]
        public void CreateLoadingPlanFromMetadata_WithOnlyEmptyFiles_ShouldReturnEmptyPlan()
        {
            // Arrange - Only empty files
            var files = new List<FileMetadata>
            {
                new("empty1.cs", "empty1.cs", 0, 1),
                new("empty2.cs", "empty2.cs", 0, 2),
                new("empty3.cs", "empty3.cs", 0, 3)
            };

            // Act
            var plan = _fileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 1000, perFileLimit: 500);

            // Assert
            Assert.That(plan.TotalFilesFound, Is.EqualTo(3), "Should count all files");
            Assert.That(plan.TotalFilesIncluded, Is.EqualTo(0), "Should include no files");
            Assert.That(plan.Items.Count, Is.EqualTo(0), "Should have no loading items");
            Assert.That(plan.BudgetUsed, Is.EqualTo(0), "Should use no budget");
        }

        [Test]
        public void CreateLoadingPlanFromMetadata_WithEmptyFilesAtBeginning_ShouldProcessRemainingFiles()
        {
            // Arrange - Empty files at the beginning (the original issue scenario)
            var files = new List<FileMetadata>
            {
                new("empty1.cs", "empty1.cs", 0, 1),     // Empty file first
                new("empty2.cs", "empty2.cs", 0, 2),     // Another empty file
                new("file1.cs", "file1.cs", 300, 3),     // Normal file
                new("file2.cs", "file2.cs", 400, 4)      // Normal file
            };

            // Act
            var plan = _fileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 1000, perFileLimit: 500);

            // Assert
            Assert.That(plan.TotalFilesFound, Is.EqualTo(4));
            Assert.That(plan.TotalFilesIncluded, Is.EqualTo(2), "Should include both non-empty files");
            Assert.That(plan.Items.Count, Is.EqualTo(2));
            
            // Verify the correct files were included
            var includedFiles = plan.Items.Select(i => i.RelativePath).ToList();
            Assert.That(includedFiles, Does.Contain("file1.cs"));
            Assert.That(includedFiles, Does.Contain("file2.cs"));
        }

        [Test]
        public void CreateLoadingPlanFromMetadata_WithManyEmptyFiles_ShouldUseFullBudgetOnValidFiles()
        {
            // Arrange - Simulate the original issue: many empty files mixed with valid ones
            var files = new List<FileMetadata>();
            
            // Add some valid files at the beginning
            files.Add(new("good1.cs", "good1.cs", 100, 1));
            files.Add(new("good2.cs", "good2.cs", 200, 2));
            
            // Add many empty files (simulating the node_modules issue)
            for (int i = 0; i < 1000; i++)
            {
                files.Add(new($"empty{i}.d.ts", $"empty{i}.d.ts", 0, i + 3));
            }
            
            // Add more valid files after the empty ones
            files.Add(new("good3.cs", "good3.cs", 150, 1003));
            files.Add(new("good4.cs", "good4.cs", 250, 1004));

            // Act - Use a larger budget to accommodate all valid files
            var plan = _fileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 1000, perFileLimit: 500);

            // Assert
            Assert.That(plan.TotalFilesFound, Is.EqualTo(1004), "Should count all files");
            Assert.That(plan.TotalFilesIncluded, Is.EqualTo(4), "Should include only the 4 non-empty files");
            Assert.That(plan.Items.Count, Is.EqualTo(4));
            
            // Verify all good files are included
            var includedFiles = plan.Items.Select(i => i.RelativePath).ToList();
            Assert.That(includedFiles, Does.Contain("good1.cs"));
            Assert.That(includedFiles, Does.Contain("good2.cs"));
            Assert.That(includedFiles, Does.Contain("good3.cs"));
            Assert.That(includedFiles, Does.Contain("good4.cs"));
            
            // Verify no empty files are included
            Assert.That(includedFiles.Any(f => f.Contains("empty")), Is.False);
            
            // Verify budget is used efficiently for valid files
            var expectedBudget = (100 + 200 + 150 + 250) + (4 * 50); // files + headers
            Assert.That(plan.BudgetUsed, Is.EqualTo(expectedBudget));
        }

        [Test]
        public void CreateLoadingPlanFromMetadata_EmptyFilesDontConsumebudget()
        {
            // Arrange
            var files = new List<FileMetadata>
            {
                new("file1.cs", "file1.cs", 100, 1),
                new("empty.cs", "empty.cs", 0, 2),       // This should not consume budget
                new("file2.cs", "file2.cs", 100, 3)
            };

            // Act - Set budget to exactly fit the two non-empty files
            var plan = _fileHelper.CreateLoadingPlanFromMetadata(files, totalBudget: 300, perFileLimit: 500);

            // Assert
            Assert.That(plan.TotalFilesIncluded, Is.EqualTo(2), "Should include both non-empty files");
            Assert.That(plan.BudgetUsed, Is.EqualTo(300), "Should use exact budget for non-empty files");
            
            var includedFiles = plan.Items.Select(i => i.RelativePath).ToList();
            Assert.That(includedFiles, Does.Contain("file1.cs"));
            Assert.That(includedFiles, Does.Contain("file2.cs"));
            Assert.That(includedFiles, Does.Not.Contain("empty.cs"));
        }

        #endregion

        #region Regex Patterns for Tests

        /// <summary>
        /// Matches file path elements in XML format: &lt;file path=
        /// </summary>
        [GeneratedRegex(@"<file path=", RegexOptions.Compiled)]
        private static partial Regex FilePathElementRegex();

        /// <summary>
        /// Matches single character 'x' for counting test purposes
        /// </summary>
        [GeneratedRegex(@"x", RegexOptions.Compiled)]
        private static partial Regex SingleCharacterXRegex();

        #endregion
    }
}
