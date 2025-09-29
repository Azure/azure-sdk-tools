// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.ClientUpdate;

public class JavaUpdateLanguageService : ClientUpdateLanguageServiceBase
{
    private readonly ILogger<JavaUpdateLanguageService> _logger;
    private readonly IClientUpdateLlmService _llmService;
    private const string CustomizationDirName = "customization";

    public JavaUpdateLanguageService(
        ILanguageSpecificCheckResolver languageSpecificCheckResolver, 
        ILogger<JavaUpdateLanguageService> logger,
        IClientUpdateLlmService llmService) : base(languageSpecificCheckResolver)
    {
        _logger = logger;
        _llmService = llmService;
    }

    public override string SupportedLanguage => "java";

    public override async Task<List<ApiChange>> DiffAsync(string oldGenerationPath, string newGenerationPath)
    {
        if (string.IsNullOrWhiteSpace(oldGenerationPath) || string.IsNullOrWhiteSpace(newGenerationPath))
        {
            throw new InvalidOperationException("Java API diff requires both oldGenerationPath and newGenerationPath.");
        }
        return await RunApiViewDiffAsync(oldGenerationPath, newGenerationPath, CancellationToken.None);
    }

    private async Task<List<ApiChange>> RunApiViewDiffAsync(string oldPath, string newPath, CancellationToken ct)
    {
        _logger.LogInformation("Running integrated Java API diff between '{Old}' and '{New}'", oldPath, newPath);
        var result = new List<ApiChange>();
        try
        {
            // var processorJarPath = await JavaApiViewJarBuilder.BuildProcessorJarAsync(_logger, ct);
            // if (string.IsNullOrEmpty(processorJarPath))
            // {
            //     _logger.LogInformation("Processor jar build returned null");
            //     return result;
            // }
            var oldInputs = DiscoverJavaInputs(oldPath);
            var newInputs = DiscoverJavaInputs(newPath);
            if (oldInputs.Count == 0 || newInputs.Count == 0)
            {
                _logger.LogInformation("No inputs for diff (old={Old}, new={New})", oldInputs.Count, newInputs.Count);
                return result;
            }
            var tempRoot = Path.Combine(Path.GetTempPath(), "apiview-diff");
            Directory.CreateDirectory(tempRoot);
            var diffOut = Path.Combine(tempRoot, "out");
            Directory.CreateDirectory(diffOut);
            if (!await RunJavaDiffCommandAsync(@"C:\Users\savaity\IdeaProjects\azure-sdk-tools\src\java\apiview-java-processor\target\apiview-java-processor-1.33.0.jar", oldInputs, newInputs, diffOut, ct))
            {
                return result;
            }
            var diffPath = Path.Combine(diffOut, "apiview-diff.json");
            if (!File.Exists(diffPath))
            {
                _logger.LogInformation("Diff output file not found: {Path}", diffPath);
                return result;
            }
            await using var fs = File.OpenRead(diffPath);
            using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("changes", out var changesElement))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new ApiChangeJsonConverter());
                var changes = JsonSerializer.Deserialize<List<ApiChange>>(changesElement.GetRawText(), options);
                if (changes != null)
                {
                    result = changes;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed running integrated diff mode");
        }
        return result;
    }

    private async Task<bool> RunJavaDiffCommandAsync(string processorJarPath, List<string> oldInputs, List<string> newInputs, string outDir, CancellationToken ct)
    {
        _logger.LogInformation("Running Java API diff command with processor jar {Jar}", processorJarPath);
        var oldJoined = string.Join(',', oldInputs);
        var newJoined = string.Join(',', newInputs);
        var psi = new ProcessStartInfo
        {
            FileName = "java",
            Arguments = $"-jar \"{processorJarPath}\" --diff --old \"{oldJoined}\" --new \"{newJoined}\" --out \"{outDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = outDir
        };
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            return false;
        }
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
        {
            _logger.LogInformation("Processor --diff exit {Code}: {Err}", proc.ExitCode, Truncate(stderr, 400));
            return false;
        }
        _logger.LogInformation("Processor --diff stdout (trunc): {Out}", Truncate(stdout, 400));
        return true;
    }

    private List<string> DiscoverJavaInputs(string generationRoot)
    {
        var inputs = new List<string>();
        try
        {
            if (Directory.Exists(generationRoot))
            {
                if (Directory.EnumerateFiles(generationRoot, "*.java", SearchOption.AllDirectories).Any())
                {
                    inputs.Add(generationRoot);
                }
                else
                {
                    _logger.LogInformation("No .java files found under provided generation root {Root}", generationRoot);
                }
            }
            else
            {
                _logger.LogInformation("Generation root does not exist: {Root}", generationRoot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Error discovering Java inputs under {Root}", generationRoot);
        }
        return inputs;
    }

    private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "...";
    
    public override string GetCustomizationRootAsync(ClientUpdateSessionState session, string generationRoot, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("GetCustomizationRootAsync called with generationRoot: '{GenerationRoot}'", generationRoot);
            var customizationSourceRoot = Path.Combine(generationRoot, CustomizationDirName, "src", "main", "java");
            var exists = Directory.Exists(customizationSourceRoot);
            _logger.LogInformation("Directory exists check result: {Exists}", exists);
            
            if (exists)
            {
                return customizationSourceRoot;
            }
            _logger.LogInformation("No customization directory found at either level, returning null");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Failed to resolve Java customization root from generationRoot '{GenerationRoot}'", generationRoot);
        }
        return null;
    }

    /// <summary>
    /// Unified method that analyzes customization impacts and generates patches in a single operation.
    /// This is the preferred method that provides both analysis and fixes together efficiently.
    /// </summary>
    public override async Task<(List<CustomizationImpact> impacts, List<PatchProposal> patches)> AnalyzeAndProposePatchesAsync(ClientUpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct)
    {
        _logger.LogInformation("Starting unified customization analysis and patch generation for {ChangeCount} changes", apiChanges.Count());
        
        if (string.IsNullOrEmpty(customizationRoot) || !Directory.Exists(customizationRoot))
        {
            _logger.LogInformation("No customization root found, returning empty results");
            return (new List<CustomizationImpact>(), new List<PatchProposal>());
        }

        var javaFiles = Directory.GetFiles(customizationRoot, "*.java", SearchOption.AllDirectories);
        if (!javaFiles.Any())
        {
            _logger.LogInformation("No Java customization files found in {Root}", customizationRoot);
            return (new List<CustomizationImpact>(), new List<PatchProposal>());
        }

        // Performance optimization: Use raw API changes for fast screening, 
        // only build structured context when overlaps are detected
        var apiChangesList = apiChanges.ToList();
        
        var allImpacts = new List<CustomizationImpact>();
        var allPatches = new List<PatchProposal>();
        
        foreach (var file in javaFiles)
        {
            try
            {
                var (impacts, patches) = await AnalyzeFileAndGeneratePatchesAsync(file, apiChangesList, ct);
                allImpacts.AddRange(impacts);
                allPatches.AddRange(patches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed unified analysis for customization file {File}", file);
            }
        }

        _logger.LogInformation("Unified analysis generated {ImpactCount} impacts and {PatchCount} patches across {FileCount} files", 
            allImpacts.Count, allPatches.Count, javaFiles.Length);

        return (allImpacts, allPatches);
    }

    /// <summary>
    /// Legacy method for backwards compatibility - delegates to unified approach.
    /// </summary>
    public override async Task<List<CustomizationImpact>> AnalyzeCustomizationImpactAsync(ClientUpdateSessionState session, string? customizationRoot, IEnumerable<ApiChange> apiChanges, CancellationToken ct)
    {
        var (impacts, _) = await AnalyzeAndProposePatchesAsync(session, customizationRoot, apiChanges, ct);
        return impacts;
    }

    /// <summary>
    /// Legacy method for backwards compatibility - delegates to unified approach.
    /// </summary>
    public override Task<List<PatchProposal>> ProposePatchesAsync(ClientUpdateSessionState session, IEnumerable<CustomizationImpact> impacts, CancellationToken ct)
    {
        _logger.LogWarning("ProposePatchesAsync is legacy in unified architecture. Consider using AnalyzeAndProposePatchesAsync instead.");
        
        // In the unified architecture, we would need the original API changes to generate patches
        // Since we don't have them in this legacy method, we return empty patches
        // The recommended approach is to use AnalyzeAndProposePatchesAsync directly
        return Task.FromResult(new List<PatchProposal>());
    }

    /// <summary>
    /// Prepares structured API change context optimized for LLM dependency chain analysis
    /// </summary>
    private StructuredApiChangeContext PrepareStructuredApiChanges(IEnumerable<ApiChange> apiChanges)
    {
        var changesList = apiChanges.ToList();

        var context = new StructuredApiChangeContext
        {
            Changes = changesList,
            ChangesByKind = changesList.GroupBy(c => c.Kind).ToDictionary(g => g.Key, g => g.ToList()),
            MethodChanges = changesList.Where(c =>
                c.Symbol.Contains("Method", StringComparison.OrdinalIgnoreCase) ||
                c.Kind.Contains("Method", StringComparison.OrdinalIgnoreCase) ||
                c.Metadata.ContainsKey("methodName")).ToList(),
            ParameterChanges = changesList.Where(c =>
                c.Kind.Contains("Parameter", StringComparison.OrdinalIgnoreCase) ||
                c.Metadata.ContainsKey("parameterNames") ||
                c.Metadata.ContainsKey("paramNameChange")).ToList(),
            TypeChanges = changesList.Where(c =>
                c.Kind.Contains("Type", StringComparison.OrdinalIgnoreCase) ||
                c.Kind.Contains("Class", StringComparison.OrdinalIgnoreCase) ||
                c.Metadata.ContainsKey("className")).ToList()
        };

        _logger.LogInformation("Prepared structured context: {TotalChanges} total changes, {MethodCount} method changes, {ParamCount} parameter changes, {TypeCount} type changes",
            context.Changes.Count, context.MethodChanges.Count, context.ParameterChanges.Count, context.TypeChanges.Count);

        return context;
    }

    /// <summary>
    /// Extracts method or class name from fully qualified name
    /// </summary>
    private string ExtractMethodOrClassNameFromFqn(string fqn)
    {
        if (string.IsNullOrEmpty(fqn))
        {
            return "";
        }
        
        var lastDotIndex = fqn.LastIndexOf('.');
        return lastDotIndex >= 0 ? fqn.Substring(lastDotIndex + 1) : fqn;
    }

    /// <summary>
    /// Unified method that analyzes a single file for impacts and generates patches in one operation.
    /// This combines dependency chain analysis with patch generation using the LLM service.
    /// </summary>
    private async Task<(List<CustomizationImpact> impacts, List<PatchProposal> patches)> AnalyzeFileAndGeneratePatchesAsync(string customizationFile, List<ApiChange> rawApiChanges, CancellationToken ct)
    {
        _logger.LogInformation("Unified analysis for file: {File}", Path.GetFileName(customizationFile));
        
        var content = await File.ReadAllTextAsync(customizationFile, ct);
        var fileName = Path.GetFileName(customizationFile);
        
        // Performance optimization: Fast screening with raw API changes first
        if (!HasPotentialDependencyChainImpact(content, rawApiChanges))
        {
            _logger.LogInformation("No potential dependency chain impacts found in {File}", fileName);
            return (new List<CustomizationImpact>(), new List<PatchProposal>());
        }

        _logger.LogInformation("Potential overlaps detected in {File}, building structured context for unified LLM analysis", fileName);
        
        // Only build expensive structured context when overlaps are detected
        var structuredChanges = PrepareStructuredApiChanges(rawApiChanges);

        // Single LLM call for both impacts and patches
        var (llmImpacts, llmPatches) = await _llmService.AnalyzeAndProposePatchesAsync(content, fileName, structuredChanges, ct);
        
        return (llmImpacts, llmPatches);
    }

    /// <summary>
    /// Quickly checks for potential overlap between raw API changes and customization file content.
    /// Based on AutoRest customization patterns: focus on symbol/metadata overlaps first, then verify customization context.
    /// Performance optimized: uses raw API changes to avoid expensive structured processing upfront.
    /// </summary>
    private bool HasPotentialDependencyChainImpact(string content, List<ApiChange> rawApiChanges)
    {
        // Primary check: Look for overlaps between API change symbols/metadata and customization content
        // This follows AutoRest pattern where customizations reference specific class/method names
        var foundSymbolOverlap = false;
        
        foreach (var change in rawApiChanges)
        {
            // Check if API change symbols appear in customization content
            if (HasApiSymbolOverlap(content, change))
            {
                foundSymbolOverlap = true;
                break;
            }
        }
        
        if (!foundSymbolOverlap)
        {
            _logger.LogInformation("No API symbol/metadata overlaps found in content");
            return false;
        }
        
        // Secondary check: Verify this is actually customization code (not just coincidental symbol matches)
        if (!IsCustomizationCode(content))
        {
            _logger.LogInformation("Found symbol overlaps but content doesn't appear to be customization code");
            return false;
        }
        
        _logger.LogInformation("Found API symbol overlaps in customization code - potential dependency chain impact");
        return true;
    }
    
    /// <summary>
    /// Checks if API change symbols/metadata overlap with customization file content.
    /// Based on AutoRest pattern: customizations reference specific class names, method names, parameter names.
    /// </summary>
    private bool HasApiSymbolOverlap(string content, ApiChange change)
    {
        // Check primary API symbol (method name, class name, etc.)
        if (!string.IsNullOrEmpty(change.Symbol) && 
            content.Contains(change.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Found API symbol overlap: {Symbol}", change.Symbol);
            return true;
        }
        
        // Check metadata for specific identifiers that customizations use
        if (change.Metadata != null)
        {
            // Method names - customizations use getMethodsByName("methodName")
            if (change.Metadata.TryGetValue("methodName", out var methodName) && 
                !string.IsNullOrEmpty(methodName) &&
                content.Contains(methodName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Found method name overlap: {MethodName}", methodName);
                return true;
            }
            
            // Class names - customizations use getClass("package", "ClassName") 
            if (change.Metadata.TryGetValue("className", out var className) && 
                !string.IsNullOrEmpty(className) &&
                content.Contains(className, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Found class name overlap: {ClassName}", className);
                return true;
            }
            
            // Parameter names - customizations reference specific parameter names in method signatures
            if (change.Metadata.TryGetValue("parameterNames", out var paramNames) && 
                !string.IsNullOrEmpty(paramNames))
            {
                var parameters = paramNames.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var param in parameters)
                {
                    var trimmedParam = param.Trim();
                    if (!string.IsNullOrEmpty(trimmedParam) && 
                        content.Contains(trimmedParam, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Found parameter name overlap: {ParamName}", trimmedParam);
                        return true;
                    }
                }
            }
            
            // FQN (Fully Qualified Name) - customizations specify full class paths
            if (change.Metadata.TryGetValue("fqn", out var fqn) && 
                !string.IsNullOrEmpty(fqn))
            {
                // Extract class name from FQN for matching
                var fqnClassName = ExtractMethodOrClassNameFromFqn(fqn);
                if (!string.IsNullOrEmpty(fqnClassName) &&
                    content.Contains(fqnClassName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Found FQN class name overlap: {ClassName} from {Fqn}", fqnClassName, fqn);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Determines if content represents actual customization code (not just coincidental symbol matches).
    /// Uses AutoRest customization library patterns.
    /// </summary>
    private bool IsCustomizationCode(string content)
    {
        // AutoRest customization library indicators
        var customizationIndicators = new[]
        {
            "extends Customization",           // Base customization class
            "LibraryCustomization",           // Main customization interface  
            "ClassCustomization",             // Class-specific customization
            "PackageCustomization",           // Package-specific customization
            "customizeAst",                   // AST manipulation method
            "getClass(",                      // Navigation to specific classes
            "getMethodsByName(",              // Method lookup by name
            "getParameterByName(",            // Parameter lookup by name
            "setJavadocComment",              // Javadoc manipulation
            "StaticJavaParser",               // JavaParser usage
            "addBlockTag",                    // Javadoc tag manipulation
            "setModifiers",                   // Modifier changes
            "addMarkerAnnotation",            // Annotation additions
            "@Override", // for public void customize method            
        };
        
        // Must have at least one strong customization indicator
        var indicatorCount = customizationIndicators.Count(indicator => 
            content.Contains(indicator, StringComparison.OrdinalIgnoreCase));
            
        if (indicatorCount > 0)
        {
            _logger.LogInformation("Detected customization code with {Count} indicators", indicatorCount);
            return true;
        }
        
        _logger.LogInformation("Content does not appear to be customization code");
        return false;
    }

    public override async Task<ValidationResult> ValidateAsync(ClientUpdateSessionState session, CancellationToken ct)
    {
        _logger.LogInformation("Starting validation of Java project after customization patches");
        
        try
        {
            // Check if we have a valid Java project structure
            var projectRoot = session.NewGeneratedPath ?? Directory.GetCurrentDirectory();
            
            // Look for Maven or Gradle build files
            var pomPath = Path.Combine(projectRoot, "pom.xml");
            var gradlePath = Path.Combine(projectRoot, "build.gradle");
            var gradleKtsPath = Path.Combine(projectRoot, "build.gradle.kts");
            
            if (!File.Exists(pomPath) && !File.Exists(gradlePath) && !File.Exists(gradleKtsPath))
            {
                return ValidationResult.CreateFailure("No Maven (pom.xml) or Gradle (build.gradle) build file found");
            }
            
            var errors = new List<string>();
            var warnings = new List<string>();
            
            // Run basic compilation check
            if (File.Exists(pomPath))
            {
                await ValidateMavenProjectAsync(projectRoot, errors, warnings, ct);
            }
            else if (File.Exists(gradlePath) || File.Exists(gradleKtsPath))
            {
                await ValidateGradleProjectAsync(projectRoot, errors, warnings, ct);
            }
            
            var result = errors.Any() 
                ? ValidationResult.CreateFailure(errors, warnings)
                : ValidationResult.CreateSuccess(warnings);
            
            _logger.LogInformation("Validation completed. Success: {Success}, Errors: {ErrorCount}", 
                result.Success, result.Errors.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation");
            return ValidationResult.CreateFailure($"Validation failed with exception: {ex.Message}");
        }
    }

    public override async Task<List<PatchProposal>> ProposeFixesAsync(ClientUpdateSessionState session, IEnumerable<string> validationErrors, CancellationToken ct)
    {
        var errorList = validationErrors.ToList();
        _logger.LogInformation("Proposing fixes for {ErrorCount} validation errors", errorList.Count);
        
        var patches = new List<PatchProposal>();
        
        if (!errorList.Any())
        {
            _logger.LogInformation("No validation errors to fix");
            return patches;
        }
        
        try
        {
            // Group errors by type for more efficient fixing
            var compilationErrors = errorList.Where(e => IsCompilationError(e)).ToList();
            var importErrors = errorList.Where(e => IsImportError(e)).ToList();
            var otherErrors = errorList.Except(compilationErrors).Except(importErrors).ToList();
            
            // Generate fixes for compilation errors (usually the most critical)
            if (compilationErrors.Any())
            {
                var compilationFixes = await GenerateCompilationFixesAsync(session, compilationErrors, ct);
                patches.AddRange(compilationFixes);
            }
            
            // Generate fixes for import errors (usually easier to fix)
            if (importErrors.Any())
            {
                var importFixes = GenerateImportFixes(session, importErrors);
                patches.AddRange(importFixes);
            }
            
            // Generate general fixes for other errors
            if (otherErrors.Any())
            {
                var generalFixes = GenerateGeneralFixes(session, otherErrors);
                patches.AddRange(generalFixes);
            }
            
            _logger.LogInformation("Generated {FixCount} fix proposals for validation errors", patches.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating validation fixes");
        }
        
        return patches;
    }

    /// <summary>
    /// Validates Maven project by running compile goal.
    /// </summary>
    private async Task ValidateMavenProjectAsync(string projectRoot, List<string> errors, List<string> warnings, CancellationToken ct)
    {
        _logger.LogInformation("Validating Maven project at {ProjectRoot}", projectRoot);
        
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "mvn";
            process.StartInfo.Arguments = "compile -q";
            process.StartInfo.WorkingDirectory = projectRoot;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync(ct);
            
            if (process.ExitCode != 0)
            {
                errors.Add($"Maven compilation failed with exit code {process.ExitCode}");
                if (!string.IsNullOrEmpty(error))
                {
                    errors.AddRange(error.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                }
            }
            
            _logger.LogInformation("Maven validation completed. Output: {Output}", output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Maven validation");
            errors.Add($"Failed to run Maven: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates Gradle project by running compileJava task.
    /// </summary>
    private async Task ValidateGradleProjectAsync(string projectRoot, List<string> errors, List<string> warnings, CancellationToken ct)
    {
        _logger.LogInformation("Validating Gradle project at {ProjectRoot}", projectRoot);
        
        try
        {
            var gradleCommand = File.Exists(Path.Combine(projectRoot, "gradlew")) ? "./gradlew" : "gradle";
            
            using var process = new Process();
            process.StartInfo.FileName = gradleCommand;
            process.StartInfo.Arguments = "compileJava -q";
            process.StartInfo.WorkingDirectory = projectRoot;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync(ct);
            
            if (process.ExitCode != 0)
            {
                errors.Add($"Gradle compilation failed with exit code {process.ExitCode}");
                if (!string.IsNullOrEmpty(error))
                {
                    errors.AddRange(error.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                }
            }
            
            _logger.LogInformation("Gradle validation completed. Output: {Output}", output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Gradle validation");
            errors.Add($"Failed to run Gradle: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates compilation fix proposals using LLM analysis.
    /// </summary>
    private Task<List<PatchProposal>> GenerateCompilationFixesAsync(ClientUpdateSessionState session, List<string> compilationErrors, CancellationToken ct)
    {
        var patches = new List<PatchProposal>();
        
        // For now, create basic guidance patches
        // In a full implementation, this would use LLM to analyze compilation errors and generate specific fixes
        
        foreach (var error in compilationErrors.Take(5)) // Limit to 5 errors to avoid overwhelming output
        {
            patches.Add(new PatchProposal
            {
                File = "compilation-fix",
                ImpactId = compilationErrors.IndexOf(error).ToString(),
                OriginalCode = "// Compilation error detected",
                FixedCode = "// Manual review required for compilation error",
                Rationale = $"Compilation error requires manual review: {error}",
                Confidence = "Low",
                Diff = $"// Compilation Error: {error}\n// Manual fix required"
            });
        }
        
        return Task.FromResult(patches);
    }

    /// <summary>
    /// Generates import fix proposals.
    /// </summary>
    private List<PatchProposal> GenerateImportFixes(ClientUpdateSessionState session, List<string> importErrors)
    {
        var patches = new List<PatchProposal>();
        
        foreach (var error in importErrors)
        {
            patches.Add(new PatchProposal
            {
                File = "import-fix",
                ImpactId = importErrors.IndexOf(error).ToString(),
                OriginalCode = "// Import error detected",
                FixedCode = "// Update import statements",
                Rationale = $"Import error may need updated import statements: {error}",
                Confidence = "Medium",
                Diff = $"// Import Error: {error}\n// Check for updated package names or new import statements"
            });
        }
        
        return patches;
    }

    /// <summary>
    /// Generates general fix proposals for other validation errors.
    /// </summary>
    private List<PatchProposal> GenerateGeneralFixes(ClientUpdateSessionState session, List<string> otherErrors)
    {
        var patches = new List<PatchProposal>();
        
        foreach (var error in otherErrors.Take(3)) // Limit to avoid spam
        {
            patches.Add(new PatchProposal
            {
                File = "general-fix",
                ImpactId = otherErrors.IndexOf(error).ToString(),
                OriginalCode = "// General validation error",
                FixedCode = "// Manual review and fix required",
                Rationale = $"General validation error needs investigation: {error}",
                Confidence = "Low",
                Diff = $"// Validation Error: {error}\n// Manual investigation required"
            });
        }
        
        return patches;
    }

    /// <summary>
    /// Determines if an error message represents a compilation error.
    /// </summary>
    private bool IsCompilationError(string error)
    {
        var compilationKeywords = new[] { "cannot find symbol", "package does not exist", "method not found", "cannot resolve", "incompatible types" };
        return compilationKeywords.Any(keyword => error.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines if an error message represents an import error.
    /// </summary>
    private bool IsImportError(string error)
    {
        return error.Contains("import", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("package does not exist", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies patches to customization files using string replacement strategy.
    /// Only applies patches with "High" confidence to ensure safety.
    /// </summary>
    public override async Task<PatchApplicationResult> ApplyPatchesAsync(ClientUpdateSessionState session, IEnumerable<PatchProposal> patches, CancellationToken ct)
    {
        var result = new PatchApplicationResult();
        var patchList = patches.ToList();
        
        // Filter to only apply high confidence patches for safety
        var highConfidencePatches = patchList.Where(p => 
            string.Equals(p.Confidence, "High", StringComparison.OrdinalIgnoreCase)).ToList();
        
        var skippedPatches = patchList.Count - highConfidencePatches.Count;
        
        result.TotalPatches = patchList.Count;

        _logger.LogInformation("Filtering patches: {TotalPatches} total, {HighConfidencePatches} high confidence, {SkippedPatches} skipped", 
            patchList.Count, highConfidencePatches.Count, skippedPatches);
        
        if (skippedPatches > 0)
        {
            _logger.LogInformation("Skipping {SkippedCount} non-high confidence patches for safety", skippedPatches);
            foreach (var skipped in patchList.Except(highConfidencePatches))
            {
                _logger.LogDebug("Skipped patch (confidence: {Confidence}): {File} - {Rationale}", 
                    skipped.Confidence, skipped.File, skipped.Rationale);
            }
        }

        _logger.LogInformation("Applying {PatchCount} high confidence patches to customization files", highConfidencePatches.Count);

        foreach (var patch in highConfidencePatches)
        {
            try
            {
                var applied = await ApplyPatchToFileAsync(session, patch, ct);
                if (applied.Success)
                {
                    result.AppliedPatches.Add($"{patch.File}: {patch.Rationale}");
                    if (!string.IsNullOrEmpty(applied.BackupFile))
                    {
                        result.BackupFiles[patch.File] = applied.BackupFile;
                    }
                    _logger.LogInformation("Successfully applied patch to {File}: {Rationale}", patch.File, patch.Rationale);
                }
                else
                {
                    result.FailedPatches.Add($"{patch.File}: {applied.Error}");
                    result.Errors.Add(applied.Error);
                    _logger.LogWarning("Failed to apply patch to {File}: {Error}", patch.File, applied.Error);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Exception applying patch to {patch.File}: {ex.Message}";
                result.FailedPatches.Add(errorMsg);
                result.Errors.Add(errorMsg);
                _logger.LogError(ex, "Exception applying patch to {File}", patch.File);
            }
        }

        result.Success = result.FailedPatchesCount == 0;
        _logger.LogInformation("Patch application completed: {Successful}/{Attempted} high confidence patches applied successfully ({Total} total patches generated)", 
            result.SuccessfulPatches, highConfidencePatches.Count, result.TotalPatches);

        return result;
    }

    /// <summary>
    /// Applies a single patch to a file using string replacement.
    /// </summary>
    private async Task<(bool Success, string Error, string BackupFile)> ApplyPatchToFileAsync(ClientUpdateSessionState session, PatchProposal patch, CancellationToken ct)
    {
        // Skip patches that are not for actual files (e.g., compilation-fix, import-fix, general-fix)
        if (string.IsNullOrEmpty(patch.File) || 
            patch.File.EndsWith("-fix") || 
            string.IsNullOrEmpty(patch.OriginalCode) || 
            string.IsNullOrEmpty(patch.FixedCode))
        {
            return (false, "Patch is not applicable to a specific file or lacks replacement content", "");
        }

        // Resolve file path relative to customization root
        var filePath = Path.IsPathRooted(patch.File) 
            ? patch.File 
            : Path.Combine(session.CustomizationRoot ?? "", patch.File);

        if (!File.Exists(filePath))
        {
            return (false, $"Target file does not exist: {filePath}", "");
        }

        try
        {
            // Read current file content
            var content = await File.ReadAllTextAsync(filePath, ct);
            
            // Create backup with timestamp
            var backupPath = $"{filePath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
            await File.WriteAllTextAsync(backupPath, content, ct);
            
            // Apply string replacement
            var newContent = content.Replace(patch.OriginalCode, patch.FixedCode);
            if (newContent == content)
            {
                // Clean up backup if no changes made
                File.Delete(backupPath);
                return (false, "Original code not found in file - no replacement occurred", "");
            }
            
            // Write modified content
            await File.WriteAllTextAsync(filePath, newContent, ct);
            
            return (true, "", backupPath);
        }
        catch (Exception ex)
        {
            return (false, $"File operation failed: {ex.Message}", "");
        }
    }
}
