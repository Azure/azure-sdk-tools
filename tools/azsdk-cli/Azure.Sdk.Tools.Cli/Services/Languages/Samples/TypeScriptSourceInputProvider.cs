// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.Languages.Samples;

public class TypeScriptSourceInputProvider : ILanguageSourceInputProvider
{
    public IReadOnlyList<SourceInput> Create(string packagePath)
    {
        var inputs = new List<SourceInput>();
        var packageJsonPath = Path.Combine(packagePath, "package.json");
        if (File.Exists(packageJsonPath)) {
            inputs.Add(new SourceInput(packageJsonPath));
        } else {
            throw new InvalidOperationException($"No valid package.json found in package path '{packagePath}'.");
        }
        var distEsmPath = Path.Combine(packagePath, "dist", "esm");
        var srcPath = Path.Combine(packagePath, "src");
        if (Directory.Exists(distEsmPath))
        {
            inputs.Add(new SourceInput(distEsmPath, IncludeExtensions: [".d.ts"]));
        }
        else if (Directory.Exists(srcPath))
        {
            inputs.Add(new SourceInput(srcPath, IncludeExtensions: [".ts"]));
        }
        else
        {
            throw new InvalidOperationException($"No valid TypeScript source directories found in package path '{packagePath}'. Expected 'dist/esm' or 'src'.");
        }

        var sampleEnvPath = Path.Combine(packagePath, "sample.env");
        if (File.Exists(sampleEnvPath))
        {
            inputs.Add(new SourceInput(sampleEnvPath));
        }

        var samplesDevPath = Path.Combine(packagePath, "samples-dev");
        if (Directory.Exists(samplesDevPath))
        {
            inputs.Add(new SourceInput(samplesDevPath, IncludeExtensions: [".ts"]));
        }

        var testSnippetsPath = Path.Combine(packagePath, "test", "snippets.spec.ts");
        if (File.Exists(testSnippetsPath))
        {
            inputs.Add(new SourceInput(testSnippetsPath));
        }

        return inputs;
    }
}
