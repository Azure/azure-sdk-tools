// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.SampleGeneration;

public class PythonSourceInputProvider : ILanguageSourceInputProvider
{
    public IReadOnlyList<FileHelper.SourceInput> Create(string packagePath)
    {
        var inputs = new List<FileHelper.SourceInput>();
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
            var testResourcesFiles = Directory.GetFiles(parentDir, "test-resources*");
            foreach (var testResourcesFile in testResourcesFiles)
            {
                inputs.Add(new FileHelper.SourceInput(testResourcesFile));
            }
        }

        return inputs;
    }
}
