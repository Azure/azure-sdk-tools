using Sdk.Tools.Cli.Models;
using Sdk.Tools.Cli.Services;
using Xunit;

namespace Sdk.Tools.Cli.Tests;

public class SdkInfoTests
{
    private readonly string _testRoot;
    
    public SdkInfoTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"SdkInfoTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }
    
    ~SdkInfoTests()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch { }
    }
    
    [Fact]
    public void Scan_DotNetProject_DetectsLanguageAndSourceFolder()
    {
        // Arrange
        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "MyProject.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(srcDir, "Program.cs"), "class Program { }");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(SdkLanguage.DotNet, info.Language);
        Assert.Equal("dotnet", info.LanguageName);
        Assert.Equal(".cs", info.FileExtension);
        Assert.Equal(srcDir, info.SourceFolder);
        Assert.True(info.IsValid);
    }
    
    [Fact]
    public void Scan_PythonProject_DetectsLanguageAndSourceFolder()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "pyproject.toml"), "[project]");
        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "main.py"), "print('hello')");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(SdkLanguage.Python, info.Language);
        Assert.Equal("python", info.LanguageName);
        Assert.Equal(".py", info.FileExtension);
        Assert.Equal(srcDir, info.SourceFolder);
    }
    
    [Fact]
    public void Scan_JavaProject_DetectsLanguageAndSourceFolder()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "pom.xml"), "<project />");
        var srcDir = Path.Combine(_testRoot, "src", "main", "java");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "Main.java"), "class Main { }");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(SdkLanguage.Java, info.Language);
        Assert.Equal("java", info.LanguageName);
        Assert.Equal(".java", info.FileExtension);
        Assert.Equal(srcDir, info.SourceFolder);
    }
    
    [Fact]
    public void Scan_TypeScriptProject_DetectsLanguageAndSourceFolder()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "tsconfig.json"), "{}");
        File.WriteAllText(Path.Combine(_testRoot, "package.json"), "{}");
        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "index.ts"), "export {}");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(SdkLanguage.TypeScript, info.Language);
        Assert.Equal("typescript", info.LanguageName);
        Assert.Equal(".ts", info.FileExtension);
    }
    
    [Fact]
    public void Scan_JavaScriptProject_WithoutTsConfig_DetectsJavaScript()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "package.json"), "{}");
        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "index.js"), "module.exports = {}");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(SdkLanguage.JavaScript, info.Language);
        Assert.Equal("javascript", info.LanguageName);
        Assert.Equal(".js", info.FileExtension);
    }
    
    [Fact]
    public void Scan_GoProject_DetectsLanguageAndSourceFolder()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "go.mod"), "module test");
        var pkgDir = Path.Combine(_testRoot, "pkg");
        Directory.CreateDirectory(pkgDir);
        File.WriteAllText(Path.Combine(pkgDir, "main.go"), "package main");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(SdkLanguage.Go, info.Language);
        Assert.Equal("go", info.LanguageName);
        Assert.Equal(".go", info.FileExtension);
    }
    
    [Fact]
    public void Scan_WithExamplesFolder_FindsSamplesFolder()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "pyproject.toml"), "[project]");
        File.WriteAllText(Path.Combine(_testRoot, "main.py"), "print('hello')");
        var examplesDir = Path.Combine(_testRoot, "examples");
        Directory.CreateDirectory(examplesDir);
        File.WriteAllText(Path.Combine(examplesDir, "sample.py"), "print('sample')");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(examplesDir, info.SamplesFolder);
        Assert.Equal(examplesDir, info.SuggestedSamplesFolder);
        Assert.Contains(examplesDir, info.AllSamplesCandidates);
    }
    
    [Fact]
    public void Scan_WithSamplesFolder_FindsSamplesFolder()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "setup.py"), "");
        File.WriteAllText(Path.Combine(_testRoot, "main.py"), "print('hello')");
        var samplesDir = Path.Combine(_testRoot, "samples");
        Directory.CreateDirectory(samplesDir);
        File.WriteAllText(Path.Combine(samplesDir, "sample.py"), "print('sample')");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(samplesDir, info.SamplesFolder);
    }
    
    [Fact]
    public void Scan_WithoutSamplesFolder_SuggestsExamples()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "setup.py"), "");
        File.WriteAllText(Path.Combine(_testRoot, "main.py"), "print('hello')");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Null(info.SamplesFolder);
        Assert.Equal(Path.Combine(_testRoot, "examples"), info.SuggestedSamplesFolder);
    }
    
    [Fact]
    public void Scan_CachesResults()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "setup.py"), "");
        File.WriteAllText(Path.Combine(_testRoot, "main.py"), "print('hello')");
        
        // Act
        var info1 = SdkInfo.Scan(_testRoot);
        var info2 = SdkInfo.Scan(_testRoot);
        
        // Assert - same reference means cached
        Assert.Same(info1, info2);
    }
    
    [Fact]
    public void ClearCache_RemovesCachedResults()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "setup.py"), "");
        File.WriteAllText(Path.Combine(_testRoot, "main.py"), "print('hello')");
        var info1 = SdkInfo.Scan(_testRoot);
        
        // Act
        SdkInfo.ClearCache();
        var info2 = SdkInfo.Scan(_testRoot);
        
        // Assert - different reference means not cached
        Assert.NotSame(info1, info2);
        // But should have same values
        Assert.Equal(info1.Language, info2.Language);
    }
    
    [Fact]
    public void DetectLanguage_QuickDetection_ReturnsCorrectLanguage()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "setup.py"), "");
        
        // Act
        var lang = SdkInfo.DetectLanguage(_testRoot);
        
        // Assert
        Assert.Equal(SdkLanguage.Python, lang);
    }
    
    [Fact]
    public void DetectLanguage_NonExistentDirectory_ReturnsNull()
    {
        // Act
        var lang = SdkInfo.DetectLanguage("/nonexistent/path/to/sdk");
        
        // Assert
        Assert.Null(lang);
    }
    
    [Fact]
    public void DetectLanguage_EmptyDirectory_ReturnsNull()
    {
        // Act
        var lang = SdkInfo.DetectLanguage(_testRoot);
        
        // Assert
        Assert.Null(lang);
    }
    
    [Fact]
    public void Scan_BuildFileInSubdir_DetectsLanguage()
    {
        // Arrange - openai-dotnet style: .csproj in src folder
        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "OpenAI.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(srcDir, "Client.cs"), "class Client { }");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert
        Assert.Equal(SdkLanguage.DotNet, info.Language);
        Assert.Equal(srcDir, info.SourceFolder);
    }
    
    [Fact]
    public void Scan_MultipleSamplesFolders_PicksBestByFileCount()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRoot, "setup.py"), "");
        File.WriteAllText(Path.Combine(_testRoot, "main.py"), "");
        
        // Create samples folder with 1 file
        var samplesDir = Path.Combine(_testRoot, "samples");
        Directory.CreateDirectory(samplesDir);
        File.WriteAllText(Path.Combine(samplesDir, "sample1.py"), "");
        
        // Create examples folder with 5 files
        var examplesDir = Path.Combine(_testRoot, "examples");
        Directory.CreateDirectory(examplesDir);
        for (int i = 1; i <= 5; i++)
            File.WriteAllText(Path.Combine(examplesDir, $"example{i}.py"), "");
        
        // Act
        var info = SdkInfo.Scan(_testRoot);
        
        // Assert - examples has more files, should be picked
        Assert.Equal(examplesDir, info.SamplesFolder);
        Assert.Contains(samplesDir, info.AllSamplesCandidates);
        Assert.Contains(examplesDir, info.AllSamplesCandidates);
    }
}
