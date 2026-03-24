# TypeSpec Namespace

Common issues and solutions for TypeSpec namespace usage and best practices.

## Namespace defaults when not explicitly set in tspconfig.yaml

When `namespace` is omitted from an emitter's config, it falls back to the `namespace` declared in the TypeSpec file. If none is declared in TypeSpec, the emitter derives a default from the resource provider name using language-specific conventions.

Management plane defaults per language: .NET → `Azure.ResourceManager.[ProviderName]`, Python → `azure-mgmt-[providername]`, Java → `com.azure.resourcemanager.[providername]`, JavaScript → `@azure/arm-[providername]`. Data plane follows the same pattern using the service group (e.g., `AI`) instead of `ResourceManager`.

To set a namespace globally for all emitters:

```yaml
namespace: Azure.LoadTesting
```
