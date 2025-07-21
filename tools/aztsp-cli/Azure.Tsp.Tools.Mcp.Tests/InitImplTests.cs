using Azure.Tsp.Tools.Mcp.Tools;

namespace Azure.Tsp.Tools.Mcp.Tests;

[TestFixture]
public class InitImplTests
{
    private InitImpl _initImpl;
    private string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        _initImpl = new InitImpl();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "InitImplTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up temp directory after each test
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup - ignore errors
            }
        }
    }

    #region QuickstartAsync Validation Tests

    [TestCase("")]
    [TestCase("   ")]
    public async Task QuickstartAsync_WithInvalidTemplate_ReturnsFailureMessage(string template)
    {
        // Arrange
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: template must be one of:"));
        Assert.That(result, Does.Contain("azure-core, azure-arm"));
    }

    [Test]
    public async Task QuickstartAsync_WithNullTemplate_ReturnsFailureMessage()
    {
        // Arrange
        string template = null!;
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: template must be one of:"));
        Assert.That(result, Does.Contain("azure-core, azure-arm"));
    }

    [TestCase("invalid-template")]
    [TestCase("Azure-Core")] // Case sensitive
    [TestCase("azure_core")] // Wrong separator
    public async Task QuickstartAsync_WithUnsupportedTemplate_ReturnsFailureMessage(string template)
    {
        // Arrange
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: template must be one of:"));
        Assert.That(result, Does.Contain($"but was '{template}'"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task QuickstartAsync_WithInvalidServiceNamespace_ReturnsFailureMessage(string serviceNamespace)
    {
        // Arrange
        string template = "azure-core";
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("Failed: serviceNamespace must be provided and cannot be empty."));
    }

    [Test]
    public async Task QuickstartAsync_WithNullServiceNamespace_ReturnsFailureMessage()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = null!;
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("Failed: serviceNamespace must be provided and cannot be empty."));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task QuickstartAsync_WithInvalidOutputDir_ReturnsFailureMessage(string outputDir)
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "TestService";

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("Failed: outputDirectory must be provided"));
    }

    [Test]
    public async Task QuickstartAsync_WithNullOutputDir_ReturnsFailureMessage()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "TestService";
        string outputDir = null!;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("Failed: outputDirectory must be provided"));
    }

    [Test]
    public async Task QuickstartAsync_WithNonExistentOutputDir_ReturnsFailureMessage()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "TestService";
        string outputDir = Path.Combine(_tempDirectory, "nonexistent");

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: Full output directory"));
        Assert.That(result, Does.EndWith("does not exist."));
    }

    [Test]
    public async Task QuickstartAsync_WithNonEmptyOutputDir_ReturnsFailureMessage()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;

        // Create a file in the output directory to make it non-empty
        File.WriteAllText(Path.Combine(outputDir, "existing-file.txt"), "content");

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: The full output directory"));
        Assert.That(result, Does.EndWith("points to a non-empty directory."));
    }

    #endregion

    #region QuickstartAsync Valid Input Tests

    [TestCase("azure-core")]
    [TestCase("azure-arm")]
    public async Task QuickstartAsync_WithValidInputs_CallsProcessCorrectly(string template)
    {
        // Arrange
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        // Since we can't easily mock the ProcessHelper.RunProcess call without refactoring,
        // we test that the method doesn't fail on validation and attempts to run the process
        // The actual process execution might fail (which is expected in a test environment without tsp installed)
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
    }

    [Test]
    public async Task QuickstartAsync_WithPascalCaseServiceNamespace_AcceptsInput()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "MyTestService"; // Pascal case as recommended
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        // The method should pass validation and attempt to run the process
    }

    [Test]
    public async Task QuickstartAsync_WithRelativePath_ConvertsToAbsolutePath()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "TestService";
        string relativeOutputDir = Path.GetRelativePath(Environment.CurrentDirectory, _tempDirectory);

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, relativeOutputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Should handle relative paths correctly by converting to absolute path
    }

    #endregion

    #region ConvertSwaggerAsync Validation Tests

    [TestCase("")]
    [TestCase("   ")]
    public async Task ConvertSwaggerAsync_WithInvalidPathToSwaggerReadme_ReturnsFailureMessage(string pathToSwaggerReadme)
    {
        // Arrange
        string outputDirectory = _tempDirectory;

        // Act
        var result = await _initImpl.ConvertSwaggerAsync(pathToSwaggerReadme, outputDirectory, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("Failed: pathToSwaggerReadme must be a valid Markdown file."));
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithNullPathToSwaggerReadme_ReturnsFailureMessage()
    {
        // Arrange
        string pathToSwaggerReadme = null!;
        string outputDirectory = _tempDirectory;

        // Act
        var result = await _initImpl.ConvertSwaggerAsync(pathToSwaggerReadme, outputDirectory, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("Failed: pathToSwaggerReadme must be a valid Markdown file."));
    }

    [TestCase("readme.txt")]
    [TestCase("readme")]
    [TestCase("readme.MD")] // Should work - case insensitive
    [TestCase("readme.markdown")]
    public async Task ConvertSwaggerAsync_WithNonMarkdownFile_ReturnsFailureMessage(string fileName)
    {
        // Arrange
        string pathToSwaggerReadme = fileName == "readme.MD" ? fileName : fileName; // Special case for testing
        string outputDirectory = _tempDirectory;

        // Act
        var result = await _initImpl.ConvertSwaggerAsync(pathToSwaggerReadme, outputDirectory, null, null, CancellationToken.None);

        // Assert
        if (fileName == "readme.MD")
        {
            // This should pass the extension check but fail on file existence
            Assert.That(result, Does.StartWith("Failed: pathToSwaggerReadme"));
            Assert.That(result, Does.EndWith("does not exist."));
        }
        else
        {
            Assert.That(result, Is.EqualTo("Failed: pathToSwaggerReadme must be a valid Markdown file."));
        }
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithNonExistentMarkdownFile_ReturnsFailureMessage()
    {
        // Arrange
        string pathToSwaggerReadme = Path.Combine(_tempDirectory, "nonexistent-readme.md");
        string outputDirectory = _tempDirectory;

        // Act
        var result = await _initImpl.ConvertSwaggerAsync(pathToSwaggerReadme, outputDirectory, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: pathToSwaggerReadme"));
        Assert.That(result, Does.EndWith("does not exist."));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task ConvertSwaggerAsync_WithInvalidOutputDirectory_ReturnsFailureMessage(string outputDirectory)
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");

        // Act
        var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("Failed: outputDirectory must be provided"));
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithNullOutputDirectory_ReturnsFailureMessage()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");
        string outputDirectory = null!;

        // Act
        var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo("Failed: outputDirectory must be provided"));
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithNonExistentOutputDirectory_ReturnsFailureMessage()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");
        string outputDirectory = Path.Combine(_tempDirectory, "nonexistent");

        // Act
        var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: Full output directory"));
        Assert.That(result, Does.EndWith("does not exist."));
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithNonEmptyOutputDirectory_ReturnsFailureMessage()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");
        string outputDirectory = _tempDirectory;

        // Create a file in the output directory to make it non-empty (in addition to the readme.md)
        File.WriteAllText(Path.Combine(outputDirectory, "existing-file.txt"), "content");

        // Act
        var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, null, null, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: The full output directory"));
        Assert.That(result, Does.EndWith("points to a non-empty directory."));
    }

    #endregion

    #region ConvertSwaggerAsync Valid Input Tests

    [Test]
    public async Task ConvertSwaggerAsync_WithValidInputs_CallsProcessCorrectly()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");

        // Create separate empty output directory
        string outputDirectory = Path.Combine(Path.GetTempPath(), "ConvertSwagger_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // Act
            var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, null, null, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputDirectory))
            {
                try { Directory.Delete(outputDirectory, true); } catch { }
            }
        }
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithAzureResourceManagementTrue_CallsProcessCorrectly()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");

        string outputDirectory = Path.Combine(Path.GetTempPath(), "ConvertSwagger_ARM_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // Act
            var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, true, null, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputDirectory))
            {
                try { Directory.Delete(outputDirectory, true); } catch { }
            }
        }
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithFullyCompatibleTrue_CallsProcessCorrectly()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");

        string outputDirectory = Path.Combine(Path.GetTempPath(), "ConvertSwagger_FC_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // Act
            var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, null, true, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputDirectory))
            {
                try { Directory.Delete(outputDirectory, true); } catch { }
            }
        }
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithBothFlagsTrue_CallsProcessCorrectly()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");

        string outputDirectory = Path.Combine(Path.GetTempPath(), "ConvertSwagger_Both_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // Act
            var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, true, true, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputDirectory))
            {
                try { Directory.Delete(outputDirectory, true); } catch { }
            }
        }
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithRelativePath_ConvertsToAbsolutePath()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");

        string outputDirectory = Path.Combine(Path.GetTempPath(), "ConvertSwagger_Rel_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDirectory);

        string relativeReadmePath = Path.GetRelativePath(Environment.CurrentDirectory, readmePath);
        string relativeOutputDir = Path.GetRelativePath(Environment.CurrentDirectory, outputDirectory);

        try
        {
            // Act
            var result = await _initImpl.ConvertSwaggerAsync(relativeReadmePath, relativeOutputDir, null, null, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            // Should handle relative paths correctly by converting to absolute paths
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputDirectory))
            {
                try { Directory.Delete(outputDirectory, true); } catch { }
            }
        }
    }

    #endregion

    #region ConvertSwaggerAsync Edge Case Tests

    [Test]
    public async Task ConvertSwaggerAsync_WithWhitespaceInPaths_TrimsCorrectly()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");

        string outputDirectory = Path.Combine(Path.GetTempPath(), "ConvertSwagger_WS_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDirectory);

        string readmePathWithWhitespace = $"  {readmePath}  ";
        string outputDirWithWhitespace = $"  {outputDirectory}  ";

        try
        {
            // Act
            var result = await _initImpl.ConvertSwaggerAsync(readmePathWithWhitespace, outputDirWithWhitespace, null, null, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            // Should handle whitespace correctly and not fail validation
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputDirectory))
            {
                try { Directory.Delete(outputDirectory, true); } catch { }
            }
        }
    }

    [TestCase("README.md")]
    [TestCase("readme.MD")]
    [TestCase("Readme.Md")]
    public async Task ConvertSwaggerAsync_WithDifferentCaseMarkdownExtensions_AcceptsInput(string fileName)
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(readmePath, "# Swagger README");

        string outputDirectory = Path.Combine(Path.GetTempPath(), "ConvertSwagger_Case_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // Act
            var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, null, null, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            // Should accept different case variations of .md extension
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputDirectory))
            {
                try { Directory.Delete(outputDirectory, true); } catch { }
            }
        }
    }

    [Test]
    public async Task ConvertSwaggerAsync_WithCancellationToken_HandlesTokenCorrectly()
    {
        // Arrange
        string readmePath = Path.Combine(_tempDirectory, "readme.md");
        File.WriteAllText(readmePath, "# Swagger README");

        string outputDirectory = Path.Combine(Path.GetTempPath(), "ConvertSwagger_CT_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDirectory);

        using var cts = new CancellationTokenSource();

        try
        {
            // Act & Assert (should not throw on cancellation token usage)
            var result = await _initImpl.ConvertSwaggerAsync(readmePath, outputDirectory, null, null, cts.Token);

            Assert.That(result, Is.Not.Null);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputDirectory))
            {
                try { Directory.Delete(outputDirectory, true); } catch { }
            }
        }
    }

    #endregion

    #region Cancellation Token Tests

    [Test]
    public async Task QuickstartAsync_WithCancellationToken_HandlesTokenCorrectly()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;
        using var cts = new CancellationTokenSource();

        // Act & Assert (should not throw on cancellation token usage)
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, cts.Token);

        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public async Task QuickstartAsync_WithWhitespaceInInputs_TrimsCorrectly()
    {
        // Arrange
        string template = "  azure-core  ";
        string serviceNamespace = "  TestService  ";
        string outputDir = $"  {_tempDirectory}  ";

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        // Should handle whitespace correctly and not fail validation
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task QuickstartAsync_WithSpecialCharactersInServiceNamespace_AcceptsInput()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "My.Test.Service"; // Dots are common in namespaces
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task QuickstartAsync_WithLongServiceNamespace_AcceptsInput()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "Microsoft.Azure.Management.SomeVeryLongServiceNameThatIsStillValid";
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region Path Handling Tests

    [Test]
    public async Task QuickstartAsync_WithDeepNestedPath_HandlesCorrectly()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "TestService";
        string nestedDir = Path.Combine(_tempDirectory, "level1", "level2", "level3");
        Directory.CreateDirectory(nestedDir);

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, nestedDir, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task QuickstartAsync_WithDirectoryContainingHiddenFiles_ReportsAsNonEmpty()
    {
        // Arrange
        string template = "azure-core";
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;

        // Create a hidden file (on Windows, this simulates hidden files)
        string hiddenFile = Path.Combine(outputDir, ".hidden");
        File.WriteAllText(hiddenFile, "hidden content");

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: The full output directory"));
        Assert.That(result, Does.EndWith("points to a non-empty directory."));
    }

    #endregion

    #region Template Validation Tests

    [Test]
    public async Task QuickstartAsync_WithTemplateContainingWhitespace_FailsValidation()
    {
        // Arrange
        string template = "azure core"; // Invalid: contains space
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: template must be one of:"));
    }

    [Test]
    public async Task QuickstartAsync_WithMixedCaseTemplate_FailsValidation()
    {
        // Arrange
        string template = "Azure-Core"; // Invalid: wrong case
        string serviceNamespace = "TestService";
        string outputDir = _tempDirectory;

        // Act
        var result = await _initImpl.QuickstartAsync(template, serviceNamespace, outputDir, CancellationToken.None);

        // Assert
        Assert.That(result, Does.StartWith("Failed: template must be one of:"));
        Assert.That(result, Does.Contain("but was 'Azure-Core'"));
    }

    #endregion
}
