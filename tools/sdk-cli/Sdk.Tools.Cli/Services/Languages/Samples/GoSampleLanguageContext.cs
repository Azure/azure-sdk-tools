// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class GoSampleLanguageContext : SampleLanguageContext
{
    public GoSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.Go;

    protected override string[] DefaultIncludeExtensions => new[] { ".go" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/vendor/**", 
        "**/*_test.go"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var path = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        
        // Deprioritize generated code - load human-written code first
        // Azure SDK Go uses zz_ prefix for generated files
        var isGenerated = path.Contains("/generated/") ||
                          name.StartsWith("zz_") ||
                          name.Contains("_autorest");
        var basePriority = isGenerated ? 100 : 0;
        
        // Within each category, prioritize key files
        if (name.Contains("client")) return basePriority + 1;
        if (name.Contains("models")) return basePriority + 2;
        return basePriority + 10;
    }

    public override string GetInstructions() =>
        "Go: package main, explicit error handling, defer cleanup, context.Context, idiomatic.";
}
