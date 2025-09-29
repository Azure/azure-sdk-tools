// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

/// <summary>
/// LLM service for analyzing customization impact and dependency chains using Azure OpenAI.
/// Follows the same pattern as ServiceRegistrations for Azure client usage.
/// </summary>
public class ClientUpdateLlmService : IClientUpdateLlmService
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly ILogger<ClientUpdateLlmService> _logger;
    private readonly string _deploymentName;

    public ClientUpdateLlmService(
        AzureOpenAIClient openAiClient, 
        ILogger<ClientUpdateLlmService> logger)
    {
        _openAiClient = openAiClient;
        _logger = logger;
        
        // Get deployment name from environment, fallback to common default
        _deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4";
        
        _logger.LogDebug("ClientUpdateLlmService initialized with deployment: {DeploymentName}", 
            _deploymentName);
    }

    /// <summary>
    /// Analyzes customization impacts and generates patch proposals in a single unified LLM operation.
    /// This is now the primary method that provides both analysis and fixes together.
    /// </summary>
    public async Task<(List<CustomizationImpact> impacts, List<PatchProposal> patches)> AnalyzeAndProposePatchesAsync(
        string customizationContent, 
        string fileName, 
        StructuredApiChangeContext structuredChanges, 
        CancellationToken ct)
    {
        _logger.LogDebug("Starting unified LLM analysis and patch generation for {File}", fileName);
        
        var impacts = new List<CustomizationImpact>();
        var patches = new List<PatchProposal>();
        
        try
        {
            // Build comprehensive prompt for combined analysis and patch generation
            var combinedPrompt = BuildDependencyChainAnalysisPrompt(customizationContent, fileName, structuredChanges);

            // Single LLM call for both impact analysis and patch proposals
            var combinedResponse = await CallLlmForDependencyAnalysisAsync(combinedPrompt, ct);

            // Parse combined response to get both impacts and patches
            var (parsedImpacts, parsedPatches) = ParseCombinedLlmResponse(combinedResponse, fileName, structuredChanges);

            impacts.AddRange(parsedImpacts);
            patches.AddRange(parsedPatches);

            _logger.LogInformation("Unified LLM analysis generated {ImpactCount} impacts and {PatchCount} patches for {File}", 
                impacts.Count, patches.Count, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed unified LLM analysis and patch generation for {File}", fileName);
        }
        
        return (impacts, patches);
    }



    public string BuildDependencyChainAnalysisPrompt(
        string customizationContent, 
        string fileName, 
        StructuredApiChangeContext structuredChanges)
    {
        var prompt = new System.Text.StringBuilder();
        
        prompt.AppendLine("# Java SDK Customization Dependency Chain Analysis");
        prompt.AppendLine();
        prompt.AppendLine("## 🚨 CRITICAL CONSTRAINT: CUSTOMIZATION-ONLY FIXES 🚨");
        prompt.AppendLine("**YOU CAN ONLY SUGGEST CHANGES TO CUSTOMIZATION CODE**");
        prompt.AppendLine("- ✅ CUSTOMIZATION FILES: Suggest fixes/improvements to these files ONLY");
        prompt.AppendLine("- ❌ GENERATED CODE: Reference material only - DO NOT suggest changes");
        prompt.AppendLine("- ❌ TSP SCHEMAS: Reference material only - DO NOT suggest changes");
        prompt.AppendLine("- ❌ BUILD/CONFIG FILES: Reference material only - DO NOT suggest changes");
        prompt.AppendLine();
        prompt.AppendLine("## Mission");
        prompt.AppendLine("Analyze how TSP schema changes break Java customization code through dependency chains.");
        prompt.AppendLine("Then suggest ONLY fixes to the customization code to adapt to those changes.");
        prompt.AppendLine("Generated code and TSP schemas are READ-ONLY references showing what changed.");
        prompt.AppendLine();
        
        prompt.AppendLine("## Critical Dependency Analysis Instructions");
        prompt.AppendLine("**ANALYZE THE ACTUAL CUSTOMIZATION CODE AGAINST THE ACTUAL API CHANGES**");
        prompt.AppendLine("Do NOT look for predefined patterns. Instead:");
        prompt.AppendLine("1. **Scan customization code** for ANY references to symbols mentioned in the API changes");
        prompt.AppendLine("2. **Cross-reference** every string literal, method call, parameter name in customization against changed symbols");
        prompt.AppendLine("3. **Identify breaking chains** where API change → customization code fails → compilation/runtime error"); 
        prompt.AppendLine("4. **Focus on actual dependencies** found in THIS specific customization file, not generic patterns");
        prompt.AppendLine("5. **Trace impact chains** from API change through generated code to customization failure point");
        prompt.AppendLine();
        
        prompt.AppendLine("## Generated Code API Changes (READ-ONLY REFERENCE)");
        prompt.AppendLine("**These changes occurred in GENERATED CODE and are reference material only**");
        prompt.AppendLine($"**Customization File Being Analyzed**: {fileName}");
        prompt.AppendLine($"**Total API Changes in Generated Code**: {structuredChanges.Changes.Count}");
        prompt.AppendLine($"**Method Changes in Generated Code**: {structuredChanges.MethodChanges.Count}");  
        prompt.AppendLine($"**Parameter Changes in Generated Code**: {structuredChanges.ParameterChanges.Count}");
        prompt.AppendLine($"**Type Changes in Generated Code**: {structuredChanges.TypeChanges.Count}");
        prompt.AppendLine();
        prompt.AppendLine("### 📋 TypeSpec Specification Changes Context");
        prompt.AppendLine("**Original TypeSpec changes that caused these API changes:**");
        prompt.AppendLine("🔗 **TSP Commit**: https://github.com/Azure/azure-rest-api-specs/commit/74d0cc137b23cbaab58baa746f182876522e88a0");
        prompt.AppendLine("📝 **Change Context**: Review this commit to understand the intent and reasoning behind the API modifications");
        prompt.AppendLine("🎯 **Analysis Guidance**: Use this commit context to better understand why certain changes were made and suggest more appropriate customization fixes");
        prompt.AppendLine();
        
        // Group changes by impact category for focused analysis
        var criticalChanges = structuredChanges.ParameterChanges.Concat(
            structuredChanges.MethodChanges.Where(c => c.Kind.Contains("Removed") || c.Kind.Contains("Modified"))).ToList();
        
        if (criticalChanges.Any())
        {
            prompt.AppendLine("### Critical Generated Code Changes (Analyze These Symbols for Customization References)");
            prompt.AppendLine("**For each symbol below, check if the customization code references it:**");
            foreach (var change in criticalChanges.Take(8)) // Focus on most critical
            {
                prompt.AppendLine($"🔍 **{change.Kind}** - Symbol: `{change.Symbol}`");
                prompt.AppendLine($"   📋 Change Detail: {change.Detail}");
                
                // Extract all searchable symbols for this change
                var searchableSymbols = new List<string> { change.Symbol };
                
                if (change.Metadata.ContainsKey("methodName"))
                {
                    var methodName = change.Metadata["methodName"];
                    searchableSymbols.Add(methodName);
                    prompt.AppendLine($"   🔎 Search customization for: \"{methodName}\"");
                }
                if (change.Metadata.ContainsKey("parameterNames"))
                {
                    var paramNames = change.Metadata["parameterNames"];
                    searchableSymbols.Add(paramNames);
                    prompt.AppendLine($"   🔎 Search customization for: \"{paramNames}\"");
                }
                if (change.Metadata.ContainsKey("paramNameChange"))
                {
                    var paramChange = change.Metadata["paramNameChange"];
                    searchableSymbols.Add(paramChange);
                    prompt.AppendLine($"   🔎 Search customization for: \"{paramChange}\"");
                }
                
                // Add the base symbol to search list
                prompt.AppendLine($"   🔎 Search customization for: \"{change.Symbol}\"");
                prompt.AppendLine($"   ❓ Question: Does the customization code reference ANY of these symbols?");
                prompt.AppendLine();
            }
        }
        
        var otherChanges = structuredChanges.Changes.Except(criticalChanges).ToList();
        if (otherChanges.Any())
        {
            prompt.AppendLine("### Other Generated Code Changes (Reference Only)");
            foreach (var change in otherChanges.Take(5))
            {
                prompt.AppendLine($"- {change.Kind}: {change.Symbol} - {change.Detail} (Generated Code)");
            }
            prompt.AppendLine();
        }
        
        prompt.AppendLine("## ✅ Customization Code Under Analysis (THIS IS WHAT YOU CAN MODIFY) ✅");
        prompt.AppendLine("**This is the CUSTOMIZATION CODE that you can suggest changes to**");
        prompt.AppendLine("```java");
        prompt.AppendLine(customizationContent);
        prompt.AppendLine("```");
        prompt.AppendLine();
        
        prompt.AppendLine("## Analysis Framework");
        prompt.AppendLine("### Step 1: Symbol Cross-Reference Analysis");
        prompt.AppendLine("- For EACH API change symbol, search the customization code for ANY reference to it");
        prompt.AppendLine("- Check string literals, method calls, variable names, comments, annotations");
        prompt.AppendLine("- Look for partial matches, concatenated strings, or derived names");
        prompt.AppendLine("- Include method parameters, return types, class names, package references");
        prompt.AppendLine();
        
        prompt.AppendLine("### Step 2: Impact Chain Tracing");  
        prompt.AppendLine("- When you find a reference, trace: API Change → Generated Code Impact → Customization Failure");
        prompt.AppendLine("- Consider compilation failures, runtime exceptions, logic breaks, assumption violations");
        prompt.AppendLine("- Analyze the ACTUAL customization intent vs. the ACTUAL API change impact");
        prompt.AppendLine();
        
        prompt.AppendLine("### Step 3: Assess Impact Severity");
        prompt.AppendLine("- **Critical**: Compilation failure, missing methods/parameters, type mismatches");
        prompt.AppendLine("- **High**: Runtime failures, logic breaks, parameter access failures"); 
        prompt.AppendLine("- **Moderate**: Behavioral changes, default value changes, optional parameter addition");
        prompt.AppendLine("- **Low**: Cosmetic changes, documentation updates, method additions");
        prompt.AppendLine();
        
        prompt.AppendLine("## Required Output Format - Combined Analysis and Patches");
        prompt.AppendLine("Return a JSON object with BOTH impact analysis AND patch proposals:");
        prompt.AppendLine("**CRITICAL**: All suggested fixes must target CUSTOMIZATION CODE ONLY");
        prompt.AppendLine();
        prompt.AppendLine("```json");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"impacts\": [");
        prompt.AppendLine("    {");
        prompt.AppendLine("      \"impactType\": \"BrokenAssumption|ParameterNameConflict|MethodSignatureChange|TypeDependencyBreak|ASTManipulationConflict\",");
        prompt.AppendLine("      \"severity\": \"Critical|High|Moderate|Low\",");
        prompt.AppendLine("      \"description\": \"Clear explanation including: dependency chain, breaking customization code, and CUSTOMIZATION-ONLY fix\",");
        prompt.AppendLine("      \"affectedSymbol\": \"Primary method/class/parameter that causes the break\",");
        prompt.AppendLine("      \"lineRange\": \"Line numbers in CUSTOMIZATION file where break occurs (e.g., '68-74')\",");
        prompt.AppendLine("      \"dependencyChain\": \"TSP change → generated impact → customization failure\",");
        prompt.AppendLine("      \"fixGuidance\": \"Instructions for modifying CUSTOMIZATION CODE to adapt to generated code changes\",");
        prompt.AppendLine("      \"breakingCode\": \"Exact customization code snippet that will break\",");
        prompt.AppendLine("      \"suggestedFix\": \"Exact customization code fix (what to change in the customization file)\"");
        prompt.AppendLine("    }");
        prompt.AppendLine("  ],");
        prompt.AppendLine("  \"patches\": [");
        prompt.AppendLine("    {");
        prompt.AppendLine("      \"impactId\": \"1\",");
        prompt.AppendLine("      \"originalCode\": \"exact original code to replace\",");
        prompt.AppendLine("      \"fixedCode\": \"exact fixed code replacement\",");
        prompt.AppendLine("      \"lineRange\": \"start-end line numbers\",");
        prompt.AppendLine("      \"rationale\": \"explanation of why this fix is needed\",");
        prompt.AppendLine("      \"confidence\": \"High|Medium|Low\"");
        prompt.AppendLine("    }");
        prompt.AppendLine("  ]");
        prompt.AppendLine("}");
        prompt.AppendLine("```");
        prompt.AppendLine();
        
        prompt.AppendLine("## Dynamic Analysis - Focus on Actual References");
        prompt.AppendLine("Based on the API changes above and the customization code provided:");
        prompt.AppendLine("- Identify WHERE in the customization code each changed symbol is referenced");
        prompt.AppendLine("- Determine HOW each reference will break (compilation, runtime, logic)");
        prompt.AppendLine("- Propose SPECIFIC fixes for each actual reference found");
        prompt.AppendLine("- Consider indirect references through string manipulation, reflection, or metadata");
        prompt.AppendLine("- Check for assumptions about API structure that may no longer hold");
        prompt.AppendLine();
        
        prompt.AppendLine("**Remember**: ");
        prompt.AppendLine("1. Focus on actual dependency chains where customization depends on changed API");
        prompt.AppendLine("2. ALL fixes must be changes to CUSTOMIZATION files only");
        prompt.AppendLine("3. Generated code changes are reference material showing what the customization must adapt to");
        
        return prompt.ToString();
    }

    public (List<CustomizationImpact> impacts, List<PatchProposal> patches) ParseCombinedLlmResponse(
        string llmResponse, 
        string fileName, 
        StructuredApiChangeContext structuredChanges)
    {
        var impacts = new List<CustomizationImpact>();
        var patches = new List<PatchProposal>();
        
        try
        {
            _logger.LogDebug("Parsing combined LLM response: {Length} characters", llmResponse.Length);
            
            // Strip markdown code fences if present
            var cleanedResponse = llmResponse.Trim();
            if (cleanedResponse.StartsWith("```json"))
            {
                cleanedResponse = cleanedResponse.Substring(7); // Remove "```json"
            }
            if (cleanedResponse.StartsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(3); // Remove "```"
            }
            if (cleanedResponse.EndsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3); // Remove trailing "```"
            }
            cleanedResponse = cleanedResponse.Trim();
            
            _logger.LogDebug("Cleaned JSON response: {Length} characters", cleanedResponse.Length);
            
            var jsonDoc = JsonDocument.Parse(cleanedResponse);
            var root = jsonDoc.RootElement;
            
            // Parse impacts section
            if (root.TryGetProperty("impacts", out var impactsArray))
            {
                foreach (var impactElement in impactsArray.EnumerateArray())
                {
                    var impact = ParseSingleLlmImpact(impactElement, fileName, structuredChanges);
                    if (impact != null)
                    {
                        impacts.Add(impact);
                        _logger.LogInformation("LLM detected impact in {File}:", fileName);
                        _logger.LogInformation("  Impact Type: {Type}", impact.ImpactType);
                        _logger.LogInformation("  Severity: {Severity}", impact.Severity);
                        _logger.LogInformation("  Affected Symbol: {Symbol}", impact.AffectedSymbol);
                        _logger.LogInformation("  Line Range: {Range}", impact.LineRange);
                        _logger.LogInformation("  Description: {Description}", 
                            impact.Description.Length > 200 ? impact.Description.Substring(0, 200) + "..." : impact.Description);
                    }
                }
            }
            
            // Parse patches section
            if (root.TryGetProperty("patches", out var patchesArray))
            {
                foreach (var patchElement in patchesArray.EnumerateArray())
                {
                    var patch = new PatchProposal
                    {
                        File = fileName,
                        ImpactId = GetJsonStringProperty(patchElement, "impactId") ?? "",
                        OriginalCode = GetJsonStringProperty(patchElement, "originalCode") ?? "",
                        FixedCode = GetJsonStringProperty(patchElement, "fixedCode") ?? "",
                        LineRange = GetJsonStringProperty(patchElement, "lineRange") ?? "",
                        Rationale = GetJsonStringProperty(patchElement, "rationale") ?? "",
                        Confidence = GetJsonStringProperty(patchElement, "confidence") ?? "Medium"
                    };
                    
                    // Generate git diff for this patch
                    patch.Diff = GenerateGitDiff(patch.OriginalCode, patch.FixedCode, fileName, patch.LineRange);
                    
                    // Log detailed information about the suggested fix
                    _logger.LogInformation("LLM suggested patch for {File} at lines {LineRange}:", fileName, patch.LineRange);
                    _logger.LogInformation("  Impact ID: {ImpactId}", patch.ImpactId);
                    _logger.LogInformation("  Confidence: {Confidence}", patch.Confidence);
                    _logger.LogInformation("  Rationale: {Rationale}", patch.Rationale);
                    _logger.LogInformation("  Original code: {OriginalCode}", 
                        patch.OriginalCode.Length > 100 ? patch.OriginalCode.Substring(0, 100) + "..." : patch.OriginalCode);
                    _logger.LogInformation("  Suggested fix: {FixedCode}", 
                        patch.FixedCode.Length > 100 ? patch.FixedCode.Substring(0, 100) + "..." : patch.FixedCode);
                    
                    patches.Add(patch);
                }
            }
            
            _logger.LogInformation("Successfully parsed {ImpactCount} impacts and {PatchCount} patches from combined LLM response", 
                impacts.Count, patches.Count);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse combined LLM response as JSON: {Response}", 
                llmResponse.Length > 200 ? llmResponse.Substring(0, 200) + "..." : llmResponse);
            
            // Try to extract useful information even from malformed JSON
            impacts.AddRange(ExtractImpactsFromMalformedResponse(llmResponse, fileName, structuredChanges));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing combined LLM response");
        }
        
        return (impacts, patches);
    }

    /// <summary>
    /// Calls the Azure OpenAI service for dependency chain analysis with retry logic.
    /// </summary>
    private async Task<string> CallLlmForDependencyAnalysisAsync(string prompt, CancellationToken ct)
    {
        _logger.LogDebug("Initiating LLM based analysis (prompt: {Length} chars)", prompt.Length);

        const int maxRetries = 3;
        const int baseDelayMs = 1000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("LLM analysis attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
                
                var response = await CallAzureOpenAiAsync(prompt, ct);
                
                if (!string.IsNullOrWhiteSpace(response))
                {
                    _logger.LogInformation("LLM analysis completed successfully on attempt {Attempt}", attempt);
                    return response;
                }
                
                _logger.LogWarning("LLM returned empty response on attempt {Attempt}", attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Network error during LLM call, attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "LLM call timeout on attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during LLM call, attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
            }
            
            // Exponential backoff for retries
            if (attempt < maxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                _logger.LogDebug("Waiting {Delay}ms before retry", delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
        }
        
        _logger.LogError("All LLM attempts failed after {MaxRetries} retries", maxRetries);
        throw new InvalidOperationException($"Failed to get response from Azure OpenAI after {maxRetries} attempts");
    }

    /// <summary>
    /// Calls Azure OpenAI using the configured client and deployment.
    /// </summary>
    private async Task<string> CallAzureOpenAiAsync(string prompt, CancellationToken ct)
    {
        try
        {
            var chatClient = _openAiClient.GetChatClient(_deploymentName);
            
            var chatMessages = new List<ChatMessage>
            {
                new SystemChatMessage("You are an expert Java developer specializing in Azure SDK customizations and code analysis. Analyze the provided code and API changes to identify potential breaking changes in customization code."),
                new UserChatMessage(prompt)
            };

            var completionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 4000,
                Temperature = 0.1f, // Low temperature for consistent, focused analysis
                FrequencyPenalty = 0,
                PresencePenalty = 0
            };

            var response = await chatClient.CompleteChatAsync(chatMessages, completionOptions, ct);
            
            var content = response.Value.Content[0].Text;
            _logger.LogDebug("Azure OpenAI response received: {Length} characters", content?.Length ?? 0);
            
            return content ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Azure OpenAI with deployment {Deployment}", _deploymentName);
            throw;
        }
    }



    /// <summary>
    /// Parses a single impact element with comprehensive error handling
    /// </summary>
    private CustomizationImpact? ParseSingleLlmImpact(JsonElement impactElement, string fileName, StructuredApiChangeContext structuredChanges)
    {
        try
        {
            var impact = new CustomizationImpact
            {
                File = fileName,
                ImpactType = GetJsonStringProperty(impactElement, "impactType") ?? "LLM_Detected",
                Severity = GetJsonStringProperty(impactElement, "severity") ?? "Moderate", 
                AffectedSymbol = GetJsonStringProperty(impactElement, "affectedSymbol") ?? "Unknown",
                LineRange = GetJsonStringProperty(impactElement, "lineRange") ?? "0"
            };
            
            // Build comprehensive description from multiple LLM fields
            var description = BuildEnhancedDescription(impactElement);
            impact.Description = description;
            
            // Find most relevant API change for this impact
            impact.ApiChange = FindRelatedApiChange(structuredChanges, impact.AffectedSymbol);
            
            return impact;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse individual impact element");
            return null;
        }
    }

    /// <summary>
    /// Builds comprehensive description from LLM response fields
    /// </summary>
    private string BuildEnhancedDescription(JsonElement impactElement)
    {
        var description = new System.Text.StringBuilder();
        
        var baseDescription = GetJsonStringProperty(impactElement, "description");
        if (!string.IsNullOrWhiteSpace(baseDescription))
        {
            description.AppendLine(baseDescription);
        }
        
        var dependencyChain = GetJsonStringProperty(impactElement, "dependencyChain");
        if (!string.IsNullOrWhiteSpace(dependencyChain))
        {
            description.AppendLine();
            description.AppendLine($"**Dependency Chain**: {dependencyChain}");
        }
        
        var breakingCode = GetJsonStringProperty(impactElement, "breakingCode");
        if (!string.IsNullOrWhiteSpace(breakingCode))
        {
            description.AppendLine();
            description.AppendLine($"**Breaking Code**: `{breakingCode}`");
        }
        
        var suggestedFix = GetJsonStringProperty(impactElement, "suggestedFix");
        if (!string.IsNullOrWhiteSpace(suggestedFix))
        {
            description.AppendLine();
            description.AppendLine($"**Suggested Fix**: `{suggestedFix}`");
        }
        
        var fixGuidance = GetJsonStringProperty(impactElement, "fixGuidance");
        if (!string.IsNullOrWhiteSpace(fixGuidance))
        {
            description.AppendLine();
            description.AppendLine($"**Fix Guidance**: {fixGuidance}");
        }
        
        return description.ToString().Trim();
    }

    /// <summary>
    /// Safely extracts string property from JSON element
    /// </summary>
    private string? GetJsonStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    /// <summary>
    /// Attempts to extract impacts even from malformed LLM responses
    /// </summary>
    private List<CustomizationImpact> ExtractImpactsFromMalformedResponse(string malformedResponse, string fileName, StructuredApiChangeContext structuredChanges)
    {
        var impacts = new List<CustomizationImpact>();
        
        try
        {
            // Try to extract basic information using regex patterns
            if (malformedResponse.Contains("Critical") || malformedResponse.Contains("High"))
            {
                impacts.Add(new CustomizationImpact
                {
                    File = fileName,
                    ImpactType = "ParseError",
                    Severity = "Moderate",
                    AffectedSymbol = "Unknown",
                    LineRange = "0",
                    Description = $"Failed to parse LLM response, but detected potential high-impact changes. Response fragment: {malformedResponse.Take(200)}...",
                    ApiChange = structuredChanges.Changes.FirstOrDefault() ?? new ApiChange()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract impacts from malformed response");
        }
        
        return impacts;
    }

    /// <summary>
    /// Finds the API change most relevant to a given symbol
    /// </summary>
    private ApiChange FindRelatedApiChange(StructuredApiChangeContext context, string affectedSymbol)
    {
        // Look for direct symbol matches first
        var directMatch = context.Changes.FirstOrDefault(c => 
            c.Symbol.Equals(affectedSymbol, StringComparison.OrdinalIgnoreCase));
        if (directMatch != null)
        {
            return directMatch;
        }
        
        // Then check metadata matches
        var metadataMatch = context.Changes.FirstOrDefault(c => 
            c.Metadata.Values.Any(v => v.Contains(affectedSymbol, StringComparison.OrdinalIgnoreCase)));
        if (metadataMatch != null)
        {
            return metadataMatch;
        }
        
        // Fallback to first change if no specific match
        return context.Changes.FirstOrDefault() ?? new ApiChange();
    }


    /// <summary>
    /// Generates a simple git-style diff for the patch.
    /// </summary>
    private string GenerateGitDiff(string originalCode, string fixedCode, string fileName, string lineRange)
    {
        var lines = lineRange.Split('-');
        var startLine = lines.Length > 0 && int.TryParse(lines[0], out var start) ? start : 1;
        
        return $@"@@ -{startLine},1 +{startLine},1 @@
-{originalCode.Trim()}
+{fixedCode.Trim()}";
    }


}
