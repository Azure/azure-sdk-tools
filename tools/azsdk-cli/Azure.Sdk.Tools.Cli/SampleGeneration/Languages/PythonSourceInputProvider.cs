// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.SampleGeneration.Languages;

public class PythonSourceInputProvider : ILanguageSourceInputProvider
{
    public IReadOnlyList<FileHelper.SourceInput> Create(string packagePath)
    {
        var inputs = new List<FileHelper.SourceInput>();

        // Prefer the explicit 'azure' folder (common in Python packages: <packageRoot>/azure/...) to avoid
        // picking up unrelated build/test artifacts that may also live at the package root. If it does not
        // exist, fall back to scanning the provided packagePath itself for .py files.
        var azureDir = Path.Combine(packagePath, "azure");
        if (Directory.Exists(azureDir))
        {
            inputs.Add(new FileHelper.SourceInput(azureDir, IncludeExtensions: [".py"]));
        }
        else
        {
            throw new ArgumentException($"The expected 'azure' directory was not found under the provided package path: '{packagePath}'.", nameof(packagePath));
        }

        var samplesDir = Path.Combine(packagePath, "samples");
        if (Directory.Exists(samplesDir))
        {
            inputs.Add(new FileHelper.SourceInput(samplesDir, IncludeExtensions: [".py"]));
        }

        var parentDir = Directory.GetParent(packagePath)?.FullName;
        if (!string.IsNullOrEmpty(parentDir))
        {
            var testResourcesFile = Path.Combine(parentDir, "test-resources.json");
            if (File.Exists(testResourcesFile))
            {
                inputs.Add(new FileHelper.SourceInput(testResourcesFile));
            }
        }

        return inputs;
    }
}
