// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents.Tools;

internal class FileToolsTests
{
    private TempDirectory? temp;
    private string baseDir => temp!.DirectoryPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        temp = TempDirectory.Create("copilot-filetoolstests");

        // Directory structure:
        // baseDir/
        //   rootfile.txt
        //   subdir/
        //     file1.txt
        //     file2.log
        //     nested/
        //       deep.txt
        File.WriteAllText(Path.Combine(baseDir, "rootfile.txt"), "Hello World\nSecond Line");
        var subdir = Path.Combine(baseDir, "subdir");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Combine(subdir, "file1.txt"), "file1 content");
        File.WriteAllText(Path.Combine(subdir, "file2.log"), "file2 content");
        var nested = Path.Combine(subdir, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "deep.txt"), "deep content");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        temp?.Dispose();
    }

    [Test]
    public async Task ReadFile_FileExists_ReturnsContent()
    {
        // Arrange
        var tool = FileTools.CreateReadFileTool(baseDir);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["filePath"] = "rootfile.txt"
        });

        // Assert
        Assert.That(result?.ToString(), Is.EqualTo("Hello World\nSecond Line"));
    }

    [Test]
    public void ReadFile_FileDoesNotExist_ThrowsArgumentException()
    {
        // Arrange
        var tool = FileTools.CreateReadFileTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.InvokeAsync(new AIFunctionArguments
            {
                ["filePath"] = "missing.txt"
            }));
        Assert.That(ex!.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void ReadFile_EmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var tool = FileTools.CreateReadFileTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.InvokeAsync(new AIFunctionArguments
            {
                ["filePath"] = ""
            }));
        Assert.That(ex!.Message, Does.Contain("cannot be null or empty"));
    }

    [Test]
    public void ReadFile_PathOutsideBaseDir_ThrowsArgumentException()
    {
        // Arrange
        var tool = FileTools.CreateReadFileTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.InvokeAsync(new AIFunctionArguments
            {
                ["filePath"] = "subdir"
            }));
        Assert.That(ex!.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void ReadFile_CustomDescription_HasCorrectDescription()
    {
        // Arrange
        var customDescription = "Read TypeSpec project files";
        var tool = FileTools.CreateReadFileTool(baseDir, description: customDescription);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(tool.Name, Is.EqualTo("ReadFile"));
            Assert.That(tool.Description, Is.EqualTo(customDescription));
        });

    }

    [Test]
    public async Task WriteFile_NewFile_CreatesAndWritesContent()
    {
        // Arrange
        var tool = FileTools.CreateWriteFileTool(baseDir);
        var newFileName = $"newfile_{Guid.NewGuid()}.txt";
        var content = "New file content";

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["filePath"] = newFileName,
            ["content"] = content
        });

        // Assert
        Assert.That(result?.ToString(), Does.Contain("Successfully wrote to"));
        var writtenContent = await File.ReadAllTextAsync(Path.Combine(baseDir, newFileName));
        Assert.That(writtenContent, Is.EqualTo(content));
    }

    [Test]
    public async Task WriteFile_ExistingFile_OverwritesContent()
    {
        // Arrange
        var tool = FileTools.CreateWriteFileTool(baseDir);
        var fileName = $"overwrite_{Guid.NewGuid()}.txt";
        var filePath = Path.Combine(baseDir, fileName);
        await File.WriteAllTextAsync(filePath, "Original content");
        var newContent = "Updated content";

        // Act
        await tool.InvokeAsync(new AIFunctionArguments
        {
            ["filePath"] = fileName,
            ["content"] = newContent
        });

        // Assert
        var writtenContent = await File.ReadAllTextAsync(filePath);
        Assert.That(writtenContent, Is.EqualTo(newContent));
    }

    [Test]
    public async Task WriteFile_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var tool = FileTools.CreateWriteFileTool(baseDir);
        var newDirName = $"newdir_{Guid.NewGuid()}";
        var newFileName = Path.Combine(newDirName, "file.txt");
        var content = "Content in new directory";

        // Act
        await tool.InvokeAsync(new AIFunctionArguments
        {
            ["filePath"] = newFileName,
            ["content"] = content
        });

        // Assert
        var writtenContent = await File.ReadAllTextAsync(Path.Combine(baseDir, newFileName));
        Assert.That(writtenContent, Is.EqualTo(content));
    }

    [Test]
    public void WriteFile_EmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var tool = FileTools.CreateWriteFileTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.InvokeAsync(new AIFunctionArguments
            {
                ["filePath"] = "",
                ["content"] = "content"
            }));
        Assert.That(ex!.Message, Does.Contain("cannot be null or empty"));
    }

    [Test]
    public void WriteFile_PathOutsideBaseDir_ThrowsArgumentException()
    {
        // Arrange
        var tool = FileTools.CreateWriteFileTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.InvokeAsync(new AIFunctionArguments
            {
                ["filePath"] = "../outside.txt",
                ["content"] = "content"
            }));
        Assert.That(ex!.Message, Does.Contain("outside the allowed base directory"));
    }

    [Test]
    public void WriteFile_CustomDescription_HasCorrectDescription()
    {
        // Arrange
        var customDescription = "Write TypeSpec customization files";
        var tool = FileTools.CreateWriteFileTool(baseDir, customDescription);

        // Assert
        Assert.That(tool.Name, Is.EqualTo("WriteFile"));
        Assert.That(tool.Description, Is.EqualTo(customDescription));
    }

    [Test]
    public async Task ListFiles_NonRecursive_ReturnsTopLevelEntries()
    {
        // Arrange
        var tool = FileTools.CreateListFilesTool(baseDir);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["directoryPath"] = ".",
            ["recursive"] = false
        });

        // Assert
        var jsonElement = (JsonElement)result!;
        var entries = jsonElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.That(entries, Is.Not.Null);
        Assert.That(entries, Does.Contain("rootfile.txt"));
        Assert.That(entries, Does.Contain("subdir"));
    }

    [Test]
    public async Task ListFiles_Recursive_ReturnsAllEntries()
    {
        // Arrange
        var tool = FileTools.CreateListFilesTool(baseDir);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["directoryPath"] = ".",
            ["recursive"] = true
        });

        // Assert
        var jsonElement = (JsonElement)result!;
        var entries = jsonElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.That(entries, Is.Not.Null);
        Assert.That(entries, Does.Contain("rootfile.txt"));
        Assert.That(entries, Does.Contain("subdir"));
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "file1.txt")));
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "file2.log")));
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "nested")));
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "nested", "deep.txt")));
    }

    [Test]
    public async Task ListFiles_FilterNonRecursive_FiltersFiles()
    {
        // Arrange
        var tool = FileTools.CreateListFilesTool(baseDir);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["directoryPath"] = ".",
            ["recursive"] = false,
            ["filter"] = "*.txt"
        });

        // Assert
        var jsonElement = (JsonElement)result!;
        var entries = jsonElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.That(entries, Is.Not.Null);
        Assert.That(entries, Does.Contain("rootfile.txt"));
        Assert.That(entries, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task ListFiles_FilterRecursive_FiltersFilesRecursively()
    {
        // Arrange
        var tool = FileTools.CreateListFilesTool(baseDir);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["directoryPath"] = ".",
            ["recursive"] = true,
            ["filter"] = "*.txt"
        });

        // Assert
        var jsonElement = (JsonElement)result!;
        var entries = jsonElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.That(entries, Is.Not.Null);
        Assert.That(entries, Does.Contain("rootfile.txt"));
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "file1.txt")));
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "nested", "deep.txt")));
        // Should not contain .log files
        Assert.That(entries, Does.Not.Contain(Path.Combine("subdir", "file2.log")));
    }

    [Test]
    public async Task ListFiles_Subdirectory_ReturnsSubdirectoryContents()
    {
        // Arrange
        var tool = FileTools.CreateListFilesTool(baseDir);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["directoryPath"] = "subdir",
            ["recursive"] = false
        });

        // Assert
        var jsonElement = (JsonElement)result!;
        var entries = jsonElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.That(entries, Is.Not.Null);
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "file1.txt")));
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "file2.log")));
        Assert.That(entries, Does.Contain(Path.Combine("subdir", "nested")));
    }

    [Test]
    public void ListFiles_DirectoryDoesNotExist_ThrowsArgumentException()
    {
        // Arrange
        var tool = FileTools.CreateListFilesTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.InvokeAsync(new AIFunctionArguments
            {
                ["directoryPath"] = "nonexistent"
            }));
        Assert.That(ex!.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void ListFiles_PathOutsideBaseDir_ThrowsArgumentException()
    {
        // Arrange
        var tool = FileTools.CreateListFilesTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.InvokeAsync(new AIFunctionArguments
            {
                ["directoryPath"] = ".."
            }));
        Assert.That(ex!.Message, Does.Contain("outside the allowed base directory"));
    }

    [Test]
    public void ListFiles_CustomDescription_HasCorrectDescription()
    {
        // Arrange
        var customDescription = "List TypeSpec files in directory";
        var tool = FileTools.CreateListFilesTool(baseDir, customDescription);

        // Assert
        Assert.That(tool.Name, Is.EqualTo("ListFiles"));
        Assert.That(tool.Description, Is.EqualTo(customDescription));
    }

    [Test]
    public async Task ListFiles_EmptyDirectoryPath_DefaultsToRoot()
    {
        // Arrange
        var tool = FileTools.CreateListFilesTool(baseDir);

        // Act - passing empty string should default to "."
        var result = await tool.InvokeAsync(new AIFunctionArguments
        {
            ["directoryPath"] = "",
            ["recursive"] = false
        });

        // Assert
        var jsonElement = (JsonElement)result!;
        var entries = jsonElement.EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.That(entries, Is.Not.Null);
        Assert.That(entries, Does.Contain("rootfile.txt"));
    }
}
