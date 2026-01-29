// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Sdk.Tools.Cli.Helpers;
using Sdk.Tools.Cli.Models;

namespace Sdk.Tools.Cli.Services.Languages.Samples;

public sealed class PythonSampleLanguageContext : SampleLanguageContext
{
    public PythonSampleLanguageContext(FileHelper fileHelper) : base(fileHelper) { }

    public override SdkLanguage Language => SdkLanguage.Python;

    protected override string[] DefaultIncludeExtensions => new[] { ".py" };

    protected override string[] DefaultExcludePatterns => new[] 
    { 
        "**/__pycache__/**", 
        "**/.*",
        "**/venv/**",
        "**/.venv/**",
        "**/*_test.py",
        "**/test_*.py"
    };

    protected override int GetPriority(FileMetadata file)
    {
        var path = file.RelativePath.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileName(file.FilePath).ToLowerInvariant();
        
        // Deprioritize generated code - load human-written code first
        var isGenerated = path.Contains("/_generated/") || 
                          path.Contains("/generated/") ||
                          name.StartsWith("_");
        var basePriority = isGenerated ? 100 : 0;
        
        // Within each category, prioritize key files
        if (name.Contains("client")) return basePriority + 1;
        if (name.Contains("_models")) return basePriority + 2;
        if (name.Contains("operations")) return basePriority + 3;
        return basePriority + 10;
    }

    public override string GetInstructions() =>
        "Python 3.9+: type hints, async/await, PEP 8, docstrings, context managers, f-strings.";
}
