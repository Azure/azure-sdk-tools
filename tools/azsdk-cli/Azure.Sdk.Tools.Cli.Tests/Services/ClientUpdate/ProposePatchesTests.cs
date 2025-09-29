using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Azure.Sdk.Tools.Cli.Tests.Services.ClientUpdate;

[TestFixture]
public class ProposePatchesTests
{
    private JavaUpdateLanguageService _service = null!;
    
    [SetUp]
    public void Setup()
    {
        var resolver = new DummyLanguageSpecificCheckResolver();
        var llmService = new TestClientUpdateLlmService();
        _service = new JavaUpdateLanguageService(resolver, NullLogger<JavaUpdateLanguageService>.Instance, llmService);
    }

    [Test]
    public async Task ProposePatchesAsync_ShouldGeneratePatches_ForParameterNameConflicts()
    {
        // Arrange - Create a temporary test file
        var tempFile = Path.Combine(Path.GetTempPath(), "TestCustomizations.java");
        var testContent = @"
public class DocumentIntelligenceCustomizations extends Customization {
    private void customizeAnalyzeOperation(LibraryCustomization customization, Logger logger) {
        String optionsParam = getParameterByName(""analyzeDocumentOptions"");
        // Some other customization code...
    }
}";
        await File.WriteAllTextAsync(tempFile, testContent);
        
        try
        {
            var session = new ClientUpdateSessionState();
            var impacts = new List<CustomizationImpact>
            {
                new CustomizationImpact
                {
                    File = tempFile,
                    ImpactType = "ParameterNameConflict",
                    Severity = "High",
                    Description = "Parameter name changed from 'analyzeDocumentOptions' to 'analyzeDocumentRequest'",
                    AffectedSymbol = "beginAnalyzeDocument",
                    LineRange = "87-87",
                    ApiChange = new ApiChange
                    {
                        Kind = "ModifiedMethodParameterNames",
                        Symbol = "beginAnalyzeDocument",
                        Detail = "analyzeDocumentOptions -> analyzeDocumentRequest"
                    }
                }
            };

            // Act
            var patches = await _service.ProposePatchesAsync(session, impacts, CancellationToken.None);

            // Assert
            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Count, Is.GreaterThan(0));
            
            var patch = patches.First();
            Assert.That(patch.File, Is.EqualTo(tempFile));
            Assert.That(patch.ImpactId, Is.Not.Empty);
            Assert.That(patch.OriginalCode, Is.Not.Empty);
            Assert.That(patch.FixedCode, Is.Not.Empty);
            Assert.That(patch.Rationale, Is.Not.Empty);
            Assert.That(patch.Confidence, Is.Not.Empty);
            Assert.That(patch.Diff, Is.Not.Empty);
            
            Console.WriteLine("=== GENERATED PATCH PROPOSAL ===");
            Console.WriteLine($"File: {Path.GetFileName(patch.File)}");
            Console.WriteLine($"Impact ID: {patch.ImpactId}");
            Console.WriteLine($"Confidence: {patch.Confidence}");
            Console.WriteLine($"Line Range: {patch.LineRange}");
            Console.WriteLine($"Rationale: {patch.Rationale}");
            Console.WriteLine($"Original: {patch.OriginalCode}");
            Console.WriteLine($"Fixed: {patch.FixedCode}");
            Console.WriteLine($"Diff:\n{patch.Diff}");
        }
        finally
        {
            // Clean up
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task ProposePatchesAsync_ShouldHandleEmptyImpacts()
    {
        // Arrange
        var session = new ClientUpdateSessionState();
        var impacts = new List<CustomizationImpact>();

        // Act
        var patches = await _service.ProposePatchesAsync(session, impacts, CancellationToken.None);

        // Assert
        Assert.That(patches, Is.Not.Null);
        Assert.That(patches, Is.Empty);
    }

    [Test]
    public async Task ProposePatchesAsync_ShouldHandleMultipleImpactsInSameFile()
    {
        // Arrange - Create a temporary test file
        var tempFile = Path.Combine(Path.GetTempPath(), "TestCustomizations2.java");
        var testContent = @"
public class DocumentIntelligenceCustomizations extends Customization {
    private void customizeAnalyzeOperation(LibraryCustomization customization, Logger logger) {
        String optionsParam = getParameterByName(""analyzeDocumentOptions"");
        var results = client.listAnalyzeBatchResults(batchId, requestOptions);
        // Some other customization code...
    }
}";
        await File.WriteAllTextAsync(tempFile, testContent);
        
        try
        {
            var session = new ClientUpdateSessionState();
            var impacts = new List<CustomizationImpact>
            {
                new CustomizationImpact
                {
                    File = tempFile,
                    ImpactType = "ParameterNameConflict",
                    Severity = "High",
                    AffectedSymbol = "beginAnalyzeDocument",
                    LineRange = "87-87"
                },
                new CustomizationImpact
                {
                    File = tempFile,
                    ImpactType = "RemovedMethodReference", 
                    Severity = "Critical",
                    AffectedSymbol = "listAnalyzeBatchResults",
                    LineRange = "120-120"
                }
            };

            // Act
            var patches = await _service.ProposePatchesAsync(session, impacts, CancellationToken.None);

            // Assert
            Assert.That(patches, Is.Not.Null);
            Assert.That(patches.Count, Is.GreaterThan(0));
            
            // All patches should be for the same file
            Assert.That(patches.All(p => p.File == tempFile), Is.True);
            
            // Should have patches for different impact types
            var patchTypes = patches.Select(p => p.Rationale).ToList();
            Console.WriteLine("Generated patch types:");
            patchTypes.ForEach(Console.WriteLine);
        }
        finally
        {
            // Clean up
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private class DummyLanguageSpecificCheckResolver : ILanguageSpecificCheckResolver
    {
        public Task<ILanguageSpecificChecks?> GetLanguageCheckAsync(string packagePath) => Task.FromResult<ILanguageSpecificChecks?>(null);
    }
}
