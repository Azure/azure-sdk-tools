// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class JavaSampleLanguageContext : SampleLanguageContext
{
    public JavaSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.Java;

    protected override string[] DefaultIncludeExtensions => new[] { ".java" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/target/**", 
        "**/build/**",
        "**/*Test.java",
        "**/*Tests.java"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var path = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        
        // Deprioritize generated code - load human-written code first
        var isGenerated = path.Contains("/generated/") ||
                          path.Contains("/implementation/") ||
                          name.Contains("generated");
        var basePriority = isGenerated ? 100 : 0;
        
        // Within each category, prioritize key files
        if (name.Contains("client")) return basePriority + 1;
        if (name.Contains("builder")) return basePriority + 2;
        if (name.Contains("model")) return basePriority + 3;
        return basePriority + 10;
    }

    public override string GetInstructions() =>
        "Java 17+: try-with-resources, Javadoc, proper exceptions, var where obvious.";
}
