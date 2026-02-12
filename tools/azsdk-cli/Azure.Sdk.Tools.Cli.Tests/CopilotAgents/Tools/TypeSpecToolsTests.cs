// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.AI;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents.Tools;

internal class TypeSpecToolsTests
{
    private TempDirectory? temp;
    private string workingDirectory => temp!.DirectoryPath;
    private Mock<INpxHelper> npxHelperMock = null!;

    [SetUp]
    public void SetUp()
    {
        temp = TempDirectory.Create("copilot-typespectoolstests");
        npxHelperMock = new Mock<INpxHelper>();
    }

    [TearDown]
    public void TearDown()
    {
        temp?.Dispose();
    }

    [Test]
    public async Task CompileTypeSpec_Success_ReturnsSuccessResult()
    {
        // Arrange
        var successOutput = "TypeSpec compilation completed successfully";
        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, successOutput)] });

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        var jsonElement = (JsonElement)result!;
        var compileResult = jsonElement.Deserialize<TypeSpecTools.CompileTypeSpecResult>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(compileResult, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(compileResult!.Success, Is.True);
            Assert.That(compileResult.Output, Is.EqualTo(successOutput));
        });

    }

    [Test]
    public async Task CompileTypeSpec_SuccessWithEmptyOutput_ReturnsDefaultMessage()
    {
        // Arrange
        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [] });

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        var jsonElement = (JsonElement)result!;
        var compileResult = jsonElement.Deserialize<TypeSpecTools.CompileTypeSpecResult>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(compileResult, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(compileResult!.Success, Is.True);
            Assert.That(compileResult.Output, Is.EqualTo("Compilation succeeded"));
        });

    }

    [Test]
    public async Task CompileTypeSpec_Failure_ReturnsFailureResult()
    {
        // Arrange
        var errorOutput = "Error: Unknown type 'InvalidType'";
        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, errorOutput)] });

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        var jsonElement = (JsonElement)result!;
        var compileResult = jsonElement.Deserialize<TypeSpecTools.CompileTypeSpecResult>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(compileResult, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(compileResult!.Success, Is.False);
            Assert.That(compileResult.Output, Is.EqualTo(errorOutput));
        });

    }

    [Test]
    public async Task CompileTypeSpec_Exception_ReturnsFailureWithExceptionMessage()
    {
        // Arrange
        var exceptionMessage = "npx not found";
        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(exceptionMessage));

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        var jsonElement = (JsonElement)result!;
        var compileResult = jsonElement.Deserialize<TypeSpecTools.CompileTypeSpecResult>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(compileResult, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(compileResult!.Success, Is.False);
            Assert.That(compileResult.Output, Does.Contain(exceptionMessage));
        });

    }

    [Test]
    public async Task CompileTypeSpec_Timeout_ReturnsTimeoutResult()
    {
        // Arrange
        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var timeout = TimeSpan.FromSeconds(30);
        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object, timeout: timeout);

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        var jsonElement = (JsonElement)result!;
        var compileResult = jsonElement.Deserialize<TypeSpecTools.CompileTypeSpecResult>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.That(compileResult, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(compileResult!.Success, Is.False);
            Assert.That(compileResult.Output, Does.Contain("timed out"));
        });

    }

    [Test]
    public async Task CompileTypeSpec_CustomEntryPoint_UsesCustomEntryPoint()
    {
        // Arrange
        var customEntryPoint = "./main.tsp";
        NpxOptions? capturedOptions = null;

        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NpxOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object, entryPoint: customEntryPoint);

        // Act
        await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        Assert.That(capturedOptions, Is.Not.Null);
        Assert.That(capturedOptions!.Args, Does.Contain(customEntryPoint));
    }

    [Test]
    public async Task CompileTypeSpec_DefaultEntryPoint_UsesClientTsp()
    {
        // Arrange
        NpxOptions? capturedOptions = null;

        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NpxOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        // Act
        await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        Assert.That(capturedOptions, Is.Not.Null);
        Assert.That(capturedOptions!.Args, Does.Contain("./client.tsp"));
    }

    [Test]
    public async Task CompileTypeSpec_UsesTypeSpecCompilerPackage()
    {
        // Arrange
        NpxOptions? capturedOptions = null;

        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NpxOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        // Act
        await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        Assert.That(capturedOptions, Is.Not.Null);
        Assert.That(capturedOptions!.Package, Is.EqualTo("@typespec/compiler"));
    }

    [Test]
    public async Task CompileTypeSpec_UsesCorrectWorkingDirectory()
    {
        // Arrange
        NpxOptions? capturedOptions = null;

        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NpxOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        // Act
        await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        Assert.That(capturedOptions, Is.Not.Null);
        Assert.That(capturedOptions!.WorkingDirectory, Is.EqualTo(workingDirectory));
    }

    [Test]
    public void CompileTypeSpec_HasCorrectToolMetadata()
    {
        // Arrange
        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(tool.Name, Is.EqualTo("CompileTypeSpec"));
            Assert.That(tool.Description, Does.Contain("Compile the TypeSpec project"));
        });

    }

    [Test]
    public async Task CompileTypeSpec_UsesDryRunFlag()
    {
        // Arrange
        NpxOptions? capturedOptions = null;

        npxHelperMock
            .Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
            .Callback<NpxOptions, CancellationToken>((options, _) => capturedOptions = options)
            .ReturnsAsync(new ProcessResult { ExitCode = 0 });

        var tool = TypeSpecTools.CreateCompileTypeSpecTool(workingDirectory, npxHelperMock.Object);

        // Act
        await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        Assert.That(capturedOptions, Is.Not.Null);
        Assert.That(capturedOptions!.Args, Does.Contain("--dry-run"));
    }
}
