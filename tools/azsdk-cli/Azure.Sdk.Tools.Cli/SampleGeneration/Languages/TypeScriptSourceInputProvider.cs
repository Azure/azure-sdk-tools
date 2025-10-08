// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.SampleGeneration.Languages;

public class TypeScriptSourceInputProvider : ILanguageSourceInputProvider
{
    public IReadOnlyList<FileHelper.SourceInput> Create(string packagePath)
    {
        var inputs = new List<FileHelper.SourceInput>();
        var distEsmPath = Path.Combine(packagePath, "dist", "esm");
        var srcPath = Path.Combine(packagePath, "src");
        if (Directory.Exists(distEsmPath))
        {
            inputs.Add(new FileHelper.SourceInput(distEsmPath, IncludeExtensions: [".d.ts"]));
        }
        else if (Directory.Exists(srcPath))
        {
            inputs.Add(new FileHelper.SourceInput(srcPath, IncludeExtensions: [".ts"]));
        }
        else
        {
            throw new InvalidOperationException($"No valid TypeScript source directories found in package path '{packagePath}'. Expected 'dist/esm' or 'src'.");
        }

        var sampleEnvPath = Path.Combine(packagePath, "sample.env");
        if (File.Exists(sampleEnvPath))
        {
            inputs.Add(new FileHelper.SourceInput(sampleEnvPath));
        }

        var samplesDevPath = Path.Combine(packagePath, "samples-dev");
        if (Directory.Exists(samplesDevPath))
        {
            inputs.Add(new FileHelper.SourceInput(samplesDevPath, IncludeExtensions: [".ts"]));
        }

        var testSnippetsPath = Path.Combine(packagePath, "test", "snippets.spec.ts");
        if (File.Exists(testSnippetsPath))
        {
            inputs.Add(new FileHelper.SourceInput(testSnippetsPath));
        }

        return inputs;
    }
}
