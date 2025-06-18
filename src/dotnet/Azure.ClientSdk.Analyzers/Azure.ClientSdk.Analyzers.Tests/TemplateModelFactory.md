# Azure.Template Model Factory Implementation

This document provides guidance on implementing a model factory for the Azure.Template project to resolve the AZC0035 analyzer warning.

## Problem

The Azure.Template project contains a `SecretBundle` class that is returned from client methods like `GetSecretValue` and `GetSecretValueAsync`. The ModelFactoryAnalyzer (AZC0035) flags this as an error because there's no corresponding model factory method for creating `SecretBundle` instances for testing/mocking purposes.

Error message:
```
/mnt/vss/_work/1/s/sdk/template/Azure.Template/src/Models/SecretBundle.cs(14,26): error AZC0035: Output model type 'SecretBundle' should have a corresponding method in a model factory class. Add a static method that returns 'SecretBundle' to a class ending with 'ModelFactory'.
```

## Solution

Add a `TemplateModelFactory` class to the Azure.Template project with a static method that creates `SecretBundle` instances.

### Implementation

1. Create a file `TemplateModelFactory.cs` in the `Azure.Template` namespace:

```csharp
using System.Collections.Generic;
using Azure.Template.Models;

namespace Azure.Template
{
    /// <summary>
    /// Model factory that creates models for mocking scenarios in Azure.Template.
    /// </summary>
    public static class TemplateModelFactory
    {
        /// <summary>
        /// Creates a new instance of <see cref="SecretBundle"/> for mocking purposes.
        /// </summary>
        /// <param name="value">The secret value.</param>
        /// <param name="id">The secret id.</param>
        /// <param name="contentType">The content type of the secret.</param>
        /// <param name="tags">Application specific metadata in the form of key-value pairs.</param>
        /// <param name="kid">If this is a secret backing a KV certificate, then this field specifies the corresponding key backing the KV certificate.</param>
        /// <param name="managed">True if the secret's lifetime is managed by key vault.</param>
        /// <returns>A new <see cref="SecretBundle"/> instance for mocking.</returns>
        public static SecretBundle SecretBundle(
            string value = null,
            string id = null,
            string contentType = null,
            IReadOnlyDictionary<string, string> tags = null,
            string kid = null,
            bool? managed = null)
        {
            return new SecretBundle(value, id, contentType, tags, kid, managed);
        }
    }
}
```

### Key Requirements

1. **Class naming**: The class must end with "ModelFactory" (e.g., `TemplateModelFactory`)
2. **Static class**: The model factory must be a static class
3. **Public accessibility**: The class and methods must be public
4. **Method naming**: The factory method should be named after the model type (e.g., `SecretBundle`)
5. **Return type**: The method must return the exact model type that client methods return

### Location

The model factory should be placed in the `src` directory of the Azure.Template project:
- Path: `sdk/template/Azure.Template/src/TemplateModelFactory.cs`
- Namespace: `Azure.Template`

## Testing

The solution includes test cases that verify:

1. The analyzer correctly flags missing model factories (`AZC0035_ProducedForAzureTemplateSecretBundle`)
2. The analyzer is satisfied when the model factory is present (`AZC0035_NotProducedForAzureTemplateWithModelFactory`)

## Verification

After implementing the model factory:

1. Build the Azure.Template project
2. Run the analyzer to confirm no AZC0035 warnings
3. Verify that the model factory method can be used for testing scenarios