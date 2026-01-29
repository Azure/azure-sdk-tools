// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class TypeScriptSampleLanguageContext : SampleLanguageContext
{
    public TypeScriptSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.TypeScript;

    protected override string[] DefaultIncludeExtensions => new[] { ".ts" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/node_modules/**", 
        "**/dist/**", 
        "**/*.d.ts",
        "**/*.test.ts",
        "**/*.spec.ts"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var path = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        
        // Deprioritize generated code - load human-written code first
        var isGenerated = path.Contains("/generated/") ||
                          name.Contains(".generated.");
        var basePriority = isGenerated ? 100 : 0;
        
        // Within each category, prioritize key files
        if (name.Contains("client")) return basePriority + 1;
        if (name.Contains("model")) return basePriority + 2;
        if (name == "index.ts") return basePriority + 3;
        return basePriority + 10;
    }

    public override string GetInstructions() =>
        "TypeScript: ES modules, async/await, strict types, const/let, arrow functions.";
}
