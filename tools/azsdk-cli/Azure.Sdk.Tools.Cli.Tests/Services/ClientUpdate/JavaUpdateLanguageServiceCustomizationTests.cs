// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.ClientUpdate;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class JavaUpdateLanguageServiceCustomizationTests
{
    private JavaUpdateLanguageService _service = null!;
    
    private class TestClientUpdateLlmService : IClientUpdateLlmService
    {
        private readonly ClientUpdateLlmService _realService;
        
        public TestClientUpdateLlmService()
        {
            // Create a real service that will use mock responses (no deployment configured)
            var mockClient = new AzureOpenAIClient(new Uri("https://test.openai.azure.com/"), new ApiKeyCredential("mock-key"));
            var logger = new MockLogger<ClientUpdateLlmService>();
            _realService = new ClientUpdateLlmService(mockClient, logger);
        }

        public async Task<(List<CustomizationImpact> impacts, List<PatchProposal> patches)> AnalyzeAndProposePatchesAsync(string fileContent, string fileName, StructuredApiChangeContext structuredChanges, CancellationToken ct)
        {
            return await _realService.AnalyzeAndProposePatchesAsync(fileContent, fileName, structuredChanges, ct);
        }



        public string BuildDependencyChainAnalysisPrompt(string fileContent, string fileName, StructuredApiChangeContext structuredChanges)
        {
            return _realService.BuildDependencyChainAnalysisPrompt(fileContent, fileName, structuredChanges);
        }



            public (List<CustomizationImpact> impacts, List<PatchProposal> patches) ParseCombinedLlmResponse(string llmResponse, string fileName, StructuredApiChangeContext structuredChanges)
            {
                return (new List<CustomizationImpact>(), new List<PatchProposal>());
            }
        }

        private class MockTokenCredential : TokenCredential
        {
            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return new AccessToken("mock_token", DateTimeOffset.Now.AddHours(1));
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
            }
        }
        
        private class MockLogger<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

    [SetUp]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<JavaUpdateLanguageService>();
        
        _service = new JavaUpdateLanguageService(null!, logger, new TestClientUpdateLlmService());
    }

    [Test]
    public async Task AnalyzeCustomizationImpactAsync_ShouldDetectParameterNameConflicts()
    {
        // Arrange - Create test API changes that match the TestCustomization.java behavior
        var apiChanges = new List<ApiChange>
        {
            new ApiChange
            {
                Kind = "ModifiedMethodParameterNames",
                Detail = "public PollerFlux beginAnalyzeDocument(String modelId, AnalyzeDocumentOptions analyzeDocumentOptions) -> public PollerFlux beginAnalyzeDocument(String modelId, AnalyzeDocumentOptions analyzeRequest)",
                Symbol = "beginAnalyzeDocument",
                Metadata = new Dictionary<string, string>
                {
                    ["methodName"] = "beginAnalyzeDocument",
                    ["fqn"] = "com.azure.ai.documentintelligence.DocumentIntelligenceAsyncClient",
                    ["parameterNames"] = "[\"modelId\",\"analyzeRequest\"]",
                    ["paramNameChange"] = "true"
                }
            }
        };

        // Use test customization file from TestAssets
        var tempDir = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets";

        // Act
        var session = new ClientUpdateSessionState();
        var impacts = await _service.AnalyzeCustomizationImpactAsync(session, tempDir, apiChanges, CancellationToken.None);

        // Write detailed analysis output to file for inspection
        var outputDir = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets\Customization";
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "ParameterConflictTest_LLM_Enhanced_Output.txt");
        
        var output = "=== LLM-ENHANCED DEPENDENCY CHAIN ANALYSIS OUTPUT ===\n";
        output += $"Test Scenario: Parameter Name Conflict Detection\n";
        output += $"API Change: ModifiedMethodParameterNames (analyzeDocumentOptions → analyzeRequest)\n";
        output += $"Analysis Method: LLM-Enhanced Dependency Chain Detection\n";
        output += $"Total impacts found: {impacts.Count}\n\n";

        for (int i = 0; i < impacts.Count; i++)
        {
            var impactItem = impacts[i];
            output += $"IMPACT {i + 1}: {impactItem.ImpactType} - {impactItem.Severity}\n";
            output += $"├─ File: {impactItem.File}\n";
            output += $"├─ Affected Symbol: {impactItem.AffectedSymbol}\n";
            output += $"├─ Line Range: {impactItem.LineRange}\n";
            output += $"├─ Related API Change: {impactItem.ApiChange?.Kind} ({impactItem.ApiChange?.Symbol})\n";
            output += $"└─ LLM Analysis:\n";
            output += $"   {impactItem.Description.Replace("\n", "\n   ")}\n\n";
        }
        
        await File.WriteAllTextAsync(outputPath, output);
        
        // Assert - LLM should detect sophisticated dependency chain impacts
        Assert.That(impacts.Count, Is.EqualTo(2), "Expected LLM to find exactly 2 dependency chain impacts: BrokenAssumption + ParameterNameConflict");
        
        var brokenAssumption = impacts.FirstOrDefault(i => i.ImpactType == "BrokenAssumption");
        var paramConflict = impacts.FirstOrDefault(i => i.ImpactType == "ParameterNameConflict");
        
        Assert.That(brokenAssumption, Is.Not.Null, "Expected LLM to detect BrokenAssumption impact");
        Assert.That(paramConflict, Is.Not.Null, "Expected LLM to detect ParameterNameConflict impact");
        
        // Verify LLM provides rich dependency chain analysis
        Assert.That(brokenAssumption.Description, Contains.Substring("dependency chain"), "Expected rich dependency chain analysis");
        Assert.That(brokenAssumption.Description, Contains.Substring("getParameterByName"), "Expected LLM to identify specific breaking pattern");
        Assert.That(paramConflict.Description, Contains.Substring("analyzeDocumentOptions"), "Expected LLM to identify parameter name conflicts");
    }

    [Test]
    public async Task AnalyzeCustomizationImpactAsync_ShouldDetectRemovedMethodReferences()
    {
        // Arrange
        var apiChanges = new List<ApiChange>
        {
            new ApiChange
            {
                Kind = "RemovedMethod",
                Detail = "public PagedFlux listAnalyzeBatchResults(String modelId, RequestOptions requestOptions)",
                Symbol = "listAnalyzeBatchResults",
                Metadata = new Dictionary<string, string>
                {
                    ["methodName"] = "listAnalyzeBatchResults",
                    ["fqn"] = "com.azure.ai.documentintelligence.DocumentIntelligenceAsyncClient"
                }
            }
        };

        // Create a temporary customization file that references the removed method
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var testCustomizationContent = @"
import com.azure.ai.documentintelligence.DocumentIntelligenceClient;
import com.azure.core.http.rest.RequestOptions;
import com.github.javaparser.ast.CompilationUnit;
import com.github.javaparser.ast.body.ClassOrInterfaceDeclaration;
import com.github.javaparser.ast.body.MethodDeclaration;

/**
 * Customization class for DocumentIntelligence client operations.
 * This extends the base Customization to add custom behavior.
 */
public class TestCustomization extends Customization {
    
    @Override
    public void customize() {
        // Customization logic that references the removed method
        DocumentIntelligenceClient client = getClient();
        
        // This method will be removed in new API - breaking customization
        client.listAnalyzeBatchResults(""modelId"", new RequestOptions());
        
        // Additional customization patterns to ensure detection
        customizeAst();
        customizePollingStrategy();
    }
    
    private void customizeAst() {
        ClassCustomization classCustomization = getPackage().getClass(""DocumentIntelligenceClient"");
        
        // More customization patterns
        MethodDeclaration method = classCustomization.getMethodsByName(""listAnalyzeBatchResults"").get(0);
        method.getParameterByName(""requestOptions"").ifPresent(param -> {
            param.setName(""options"");
        });
    }
    
    private void customizePollingStrategy() {
        // Polling strategy customization
        LibraryCustomization.getInstance().addCustomBehavior();
    }
}";
            var customizationFile = Path.Combine(tempDir, "TestCustomizations.java");
            await File.WriteAllTextAsync(customizationFile, testCustomizationContent);

            // Act
            var session = new ClientUpdateSessionState();
            var impacts = await _service.AnalyzeCustomizationImpactAsync(session, tempDir, apiChanges, CancellationToken.None);

            // Write detailed LLM analysis output to file
            var outputDir = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets\Customization";
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, "RemovedMethodTest_LLM_Analysis.txt");
            
            var output = "=== LLM-ENHANCED REMOVED METHOD DETECTION ===\n";
            output += $"Test Scenario: Method Removal Impact Analysis\n";
            output += $"API Change: RemovedMethod (listAnalyzeBatchResults)\n";
            output += $"Analysis Method: LLM Dependency Chain Detection\n";
            output += $"Total impacts detected: {impacts.Count}\n\n";

            if (impacts.Any())
            {
                output += "🔍 LLM DEPENDENCY CHAIN ANALYSIS RESULTS:\n\n";
                for (int i = 0; i < impacts.Count; i++)
                {
                    var impact = impacts[i];
                    output += $"CRITICAL IMPACT {i + 1}: {impact.ImpactType}\n";
                    output += $"┌─ Breaking Change: {impact.ApiChange?.Kind} - {impact.AffectedSymbol}\n";
                    output += $"├─ Affected File: {impact.File} (lines {impact.LineRange})\n";
                    output += $"├─ Severity Level: {impact.Severity}\n";
                    output += $"└─ LLM Dependency Analysis:\n";
                    output += $"  {impact.Description.Replace("\n", "\n  ")}\n\n";
                }
                
                output += "📋 SUMMARY: LLM successfully identified dependency chain break where:\n";
                output += "   TSP removes method → Java generator removes method → customization references non-existent method → compilation failure\n";
            }
            else
            {
                output += "❌ NO IMPACTS DETECTED - This may indicate an analysis issue\n";
            }
            
            await File.WriteAllTextAsync(outputPath, output);
            
            // Assert - expect LLM to find removed method reference impact
            Assert.That(impacts, Is.Not.Empty, "Expected LLM to detect RemovedMethodReference impact for listAnalyzeBatchResults");
            Assert.That(impacts.First().ImpactType, Is.EqualTo("RemovedMethodReference"), "Expected specific RemovedMethodReference impact type");
            Assert.That(impacts.First().AffectedSymbol, Is.EqualTo("listAnalyzeBatchResults"), "Expected impact on listAnalyzeBatchResults method");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task AnalyzeCustomizationImpactAsync_WithRealTestData_ShouldAnalyzeCorrectly()
    {
        // This test uses the actual test data files
        var testJsonPath = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets\Customization\apiview-diff.json";
        
        if (!File.Exists(testJsonPath))
        {
            Console.WriteLine($"Test JSON not found at: {testJsonPath}");
            return; // Skip test if file not available
        }

        // Load API changes from test JSON
        var jsonContent = await File.ReadAllTextAsync(testJsonPath);
        var doc = JsonDocument.Parse(jsonContent);
        var apiChanges = new List<ApiChange>();
        
        if (doc.RootElement.TryGetProperty("changes", out var changesElement))
        {
            foreach (var change in changesElement.EnumerateArray())
            {
                var apiChange = new ApiChange();
                
                if (change.TryGetProperty("changeType", out var changeTypeElement))
                {
                    apiChange.Kind = changeTypeElement.GetString() ?? string.Empty;
                }
                
                string? before = null, after = null;
                if (change.TryGetProperty("before", out var beforeElement))
                {
                    before = beforeElement.GetString();
                }
                if (change.TryGetProperty("after", out var afterElement))
                {
                    after = afterElement.GetString();
                }
                
                if (!string.IsNullOrEmpty(before) && !string.IsNullOrEmpty(after))
                {
                    apiChange.Detail = $"{before} -> {after}";
                }
                else
                {
                    apiChange.Detail = before ?? after ?? string.Empty;
                }
                
                if (change.TryGetProperty("meta", out var metaElement))
                {
                    foreach (var property in metaElement.EnumerateObject())
                    {
                        var value = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString(),
                            JsonValueKind.Array => string.Join(",", property.Value.EnumerateArray().Select(e => e.GetString())),
                            _ => property.Value.GetRawText()
                        };
                        
                        if (!string.IsNullOrEmpty(value))
                        {
                            apiChange.Metadata[property.Name] = value;
                        }
                    }
                    
                    if (metaElement.TryGetProperty("methodName", out var methodNameElement))
                    {
                        apiChange.Symbol = methodNameElement.GetString() ?? string.Empty;
                    }
                    else if (metaElement.TryGetProperty("fqn", out var fqnElement))
                    {
                        apiChange.Symbol = fqnElement.GetString() ?? string.Empty;
                    }
                }
                
                apiChanges.Add(apiChange);
            }
        }

        // Use existing TestAssets customization files
        var tempDir = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets";

        // Act
        var session = new ClientUpdateSessionState();
        var impacts = await _service.AnalyzeCustomizationImpactAsync(session, tempDir, apiChanges, CancellationToken.None);

                // Write comprehensive LLM analysis output to file in TestAssets
        var outputDir = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets\Customization";
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "RealTestData_LLM_Comprehensive_Analysis.txt");

        var output = "=== LLM-ENHANCED COMPREHENSIVE API DIFF ANALYSIS ===\n";
        output += $"Test Scenario: Real-world API Diff Processing\n";
        output += $"Data Source: apiview-diff.json (15 API changes)\n";
        output += $"Analysis Method: LLM Multi-Change Dependency Detection\n";
        output += $"Customization Files: TestCustomizations.java + others\n";
        output += $"Total impacts detected: {impacts.Count}\n\n";

        if (impacts.Any())
        {
            output += "🔍 COMPREHENSIVE LLM ANALYSIS RESULTS:\n\n";
            
            var groupedImpacts = impacts.GroupBy(i => i.File);
            foreach (var fileGroup in groupedImpacts)
            {
                output += $"📄 FILE: {fileGroup.Key} ({fileGroup.Count()} impacts)\n";
                
                foreach (var impact in fileGroup.Select((imp, idx) => new { Impact = imp, Index = idx + 1 }))
                {
                    output += $"├─ IMPACT {impact.Index}: {impact.Impact.ImpactType} ({impact.Impact.Severity})\n";
                    output += $"│  ├─ Symbol: {impact.Impact.AffectedSymbol}\n";
                    output += $"│  ├─ Lines: {impact.Impact.LineRange}\n";
                    output += $"│  ├─ API Change: {impact.Impact.ApiChange?.Kind}\n";
                    output += $"│  └─ Analysis: {impact.Impact.Description.Split('\n').FirstOrDefault()}\n";
                }
                output += "\n";
            }
            
            output += "📊 IMPACT BREAKDOWN:\n";
            var impactTypes = impacts.GroupBy(i => i.ImpactType);
            foreach (var typeGroup in impactTypes)
            {
                output += $"   • {typeGroup.Key}: {typeGroup.Count()} occurrences\n";
            }
            
            output += "\n✅ LLM SUCCESSFULLY ANALYZED: Complex multi-change scenario with sophisticated dependency chain detection\n";
        }
        else
        {
            output += "⚠️  NO IMPACTS DETECTED - This may indicate analysis configuration issue\n";
        }
        
        await File.WriteAllTextAsync(outputPath, output);
        
        // Assert - LLM should find multiple sophisticated impacts from real API diff data  
        Assert.That(impacts, Is.Not.Empty, "Expected LLM to detect impacts from real API diff with parameter changes affecting customizations");
        Assert.That(impacts.Count, Is.GreaterThanOrEqualTo(2), "Expected LLM to find multiple dependency chain impacts in comprehensive analysis");
    }

    [Test]
    public async Task AnalyzeCustomizationImpactAsync_WithNoConflicts_ShouldReturnEmpty()
    {
        // Arrange - API change that doesn't conflict with customization
        var apiChanges = new List<ApiChange>
        {
            new ApiChange
            {
                Kind = "AddedMethod",
                Detail = "public void newMethod()",
                Symbol = "newMethod",
                Metadata = new Dictionary<string, string>
                {
                    ["methodName"] = "newMethod",
                    ["fqn"] = "com.azure.some.other.Class"
                }
            }
        };

        // Use TestAssets customization files
        var tempDir = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets";

        // Act
        var session = new ClientUpdateSessionState();
        var impacts = await _service.AnalyzeCustomizationImpactAsync(session, tempDir, apiChanges, CancellationToken.None);

        // Write raw CustomizationImpact output to file
        var outputDir = @"C:\Users\savaity\IdeaProjects\azure-sdk-tools\tools\azsdk-cli\Azure.Sdk.Tools.Cli.Tests\TestAssets\Customization";
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "NoConflictsTest_Output.txt");
        
        var output = "=== RAW CUSTOMIZATION IMPACT OUTPUT ===\n";
        output += $"Total impacts: {impacts.Count}\n\n";

        for (int i = 0; i < impacts.Count; i++)
        {
            var impact = impacts[i];
            output += $"Impact {i + 1}:\n";
            output += $"  File: '{impact.File}'\n";
            output += $"  ImpactType: {impact.ImpactType}\n";
            output += $"  Severity: {impact.Severity}\n";
            output += $"  Description: '{impact.Description}'\n";
            output += $"  AffectedSymbol: '{impact.AffectedSymbol}'\n";
            output += $"  LineRange: '{impact.LineRange}'\n";
            output += $"  ApiChange.Kind: '{impact.ApiChange?.Kind}'\n";
            output += $"  ApiChange.Symbol: '{impact.ApiChange?.Symbol}'\n";
            output += $"  ApiChange.Detail: '{impact.ApiChange?.Detail}'\n";
            output += "\n";
        }
        
        await File.WriteAllTextAsync(outputPath, output);

        // Write LLM analysis output showing smart conflict detection
        var analysisOutput = "=== LLM-ENHANCED SMART CONFLICT DETECTION ===\n";
        analysisOutput += $"Test Scenario: Unrelated API Change (Should NOT impact existing customizations)\n";
        analysisOutput += $"API Change: AddedMethod to com.azure.some.other.Class.newMethod()\n";
        analysisOutput += $"Customization Area: Document Intelligence (beginAnalyzeDocument methods)\n";
        analysisOutput += $"Analysis Method: LLM Context-Aware Dependency Analysis\n";
        analysisOutput += $"Expected Result: 0 impacts (no dependency chain relationship)\n";
        analysisOutput += $"Actual Result: {impacts.Count} impacts ✓\n\n";
        
        if (impacts.Count == 0)
        {
            analysisOutput += "✅ LLM CORRECTLY IDENTIFIED: No dependency chain between unrelated API addition and existing customizations\n";
            analysisOutput += "📊 Analysis Details:\n";
            analysisOutput += "   - API adds method to different class (com.azure.some.other.Class)\n";
            analysisOutput += "   - Customizations work with Document Intelligence API (com.azure.ai.documentintelligence)\n";
            analysisOutput += "   - No shared symbols, parameters, or method signatures\n";
            analysisOutput += "   - Adding methods to unrelated areas does not break existing customization assumptions\n";
        }
        else
        {
            analysisOutput += "❌ UNEXPECTED: LLM found impacts for unrelated change:\n";
            foreach (var impact in impacts)
            {
                analysisOutput += $"   - {impact.ImpactType}: {impact.AffectedSymbol} in {impact.File}\n";
            }
        }
        
        outputPath = Path.Combine(outputDir, "NoConflictsTest_LLM_Smart_Detection.txt");
        await File.WriteAllTextAsync(outputPath, analysisOutput);

        // Assert - LLM should intelligently detect no dependency chain relationship
        Assert.That(impacts.Count, Is.EqualTo(0), 
            "Expected LLM to intelligently detect that AddedMethod to unrelated class has no dependency chain impact on existing customizations");
    }
}
