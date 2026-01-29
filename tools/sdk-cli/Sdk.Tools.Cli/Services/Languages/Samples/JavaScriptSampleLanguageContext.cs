// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class JavaScriptSampleLanguageContext : SampleLanguageContext
{
    public JavaScriptSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.JavaScript;

    protected override string[] DefaultIncludeExtensions => new[] { ".js", ".mjs", ".cjs" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/node_modules/**", 
        "**/dist/**", 
        "**/build/**",
        "**/*.test.js",
        "**/*.spec.js"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var path = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        
        // Deprioritize generated code - load human-written code first
        var isGenerated = path.Contains("/generated/") ||
                          path.Contains("/dist/") ||
                          name.Contains(".generated.");
        var basePriority = isGenerated ? 100 : 0;
        
        // Within each category, prioritize key files
        if (name.Contains("client")) return basePriority + 1;
        if (name.Contains("model")) return basePriority + 2;
        if (name == "index.js") return basePriority + 3;
        return basePriority + 10;
    }

    public override string GetInstructions() =>
        "JavaScript: ES modules, async/await, const/let, arrow functions, JSDoc.";
}
