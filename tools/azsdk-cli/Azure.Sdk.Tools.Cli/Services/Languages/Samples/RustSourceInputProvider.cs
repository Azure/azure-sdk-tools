// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.Languages.Samples;

public class RustSourceInputProvider(string workspacePath) : ILanguageSourceInputProvider
{
    public IReadOnlyList<SourceInput> Create(string packagePath)
    {
        var sources = new List<SourceInput>();

        var src = Path.Combine(packagePath, "src");
        if (Directory.Exists(src))
        {
            sources.Add(new SourceInput(src, IncludeExtensions: [".rs"]));
        }

        var tests = Path.Combine(packagePath, "tests");
        if (Directory.Exists(tests))
        {
            sources.Add(new SourceInput(tests, IncludeExtensions: [".rs"]));
        }

        var examples = Path.Combine(packagePath, "examples");
        if (Directory.Exists(examples)) 
        {
            sources.Add(new SourceInput(examples, IncludeExtensions: [".rs"]));
        }

        var workspace = Path.Combine(workspacePath, "Cargo.toml");
        if (File.Exists(workspace))
        {
            sources.Add(new SourceInput(workspace));
        }

        return sources;
    }
}
