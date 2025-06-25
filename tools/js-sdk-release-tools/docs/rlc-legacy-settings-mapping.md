# RLC Pipeline with Legacy Settings Mapping

This example demonstrates how to use the Legacy Settings Mapping feature specifically with Rest Level Client (RLC) generation.

## RLC TypeSpec Configuration Example

### Old Configuration (tspconfig.yaml)

```yaml
# Legacy RLC configuration with old setting names
parameters:
  old-service-dir:
    default: "sdk/widgets"
  old-package-dir: 
    default: "widgets-rest"

options:
  "@azure-tools/typespec-ts":
    legacy-emitter-name: "old-emitter-value"
    package-name: "@azure-rest/widgets"
    flavor: "azure"
    generate-metadata: true
```

## Usage with RLC Pipeline

### Command Line Examples

```bash
# Enable legacy settings mapping for RLC generation
node autoGenerateInPipeline.js \
  --inputJsonPath input.json \
  --outputJsonPath output.json \
  --typespecEmitter "@azure-tools/typespec-ts" \
  --enableLegacySettingsMapping

# Using environment variable
export ENABLE_LEGACY_SETTINGS_MAPPING=true
node autoGenerateInPipeline.js \
  --inputJsonPath input.json \
  --outputJsonPath output.json \
  --typespecEmitter "@azure-tools/typespec-ts"
```

### Input JSON Structure for RLC

```json
{
  "sdkType": "RestLevelClient",
  "specFolder": "/path/to/spec/repo",
  "typespecProject": "specification/widgets/Widgets",
  "gitCommitId": "abc123",
  "repoHttpsUrl": "https://github.com/Azure/azure-rest-api-specs.git"
}
```

## What Happens During RLC Processing

When legacy settings mapping is enabled for RLC generation, the old configuration gets transformed:

```yaml
# Effective configuration used internally
parameters:
  service-dir:
    default: "sdk/widgets"
  package-dir:
    default: "widgets-rest"

options:
  "@azure-tools/typespec-ts":
    "@azure-tools/typespec-ts": "old-emitter-value"
    package-name: "@azure-rest/widgets"
    flavor: "azure"
    generate-metadata: true
```

## RLC-Specific Features

The legacy settings mapping for RLC includes:

1. **TypeSpec Project Processing**: Applies mapping when resolving TypeSpec configuration
2. **Package Directory Resolution**: Maps old directory settings to new structure
3. **Emitter Configuration**: Updates emitter-specific options
4. **REST Client Generation**: Maintains compatibility during code generation

## Log Output for RLC

When the feature runs with RLC, you'll see:

```text
[INFO]: Start to generate rest level client SDK from 'specification/widgets/Widgets'
[INFO]: Applying legacy settings mapping...
[INFO]: Mapping legacy parameter 'old-service-dir' to 'service-dir'
[INFO]: Mapping legacy parameter 'old-package-dir' to 'package-dir'
[INFO]: Mapping legacy emitter option 'legacy-emitter-name' to '@azure-tools/typespec-ts' for emitter '@azure-tools/typespec-ts'
[INFO]: Legacy settings mapping completed.
[INFO]: Generated code by tsp-client successfully.
```

## RLC vs Modular Client

Both SDK types now support legacy settings mapping:

| Feature | Modular Client | Rest Level Client |
|---------|---------------|-------------------|
| Legacy Settings Mapping | ✅ Full Support | ✅ Full Support |
| Command Line Parameter | ✅ | ✅ |
| Environment Variable | ✅ | ✅ |
| TypeSpec Configuration | ✅ | ✅ |
| Package Directory Mapping | ✅ | ✅ |

## Migration for RLC Projects

1. **Enable the feature** during migration period
2. **Update tspconfig.yaml** files to use new setting names
3. **Test both old and new configurations** work correctly
4. **Gradually migrate** RLC projects to new settings
5. **Disable the feature** once migration is complete

## Notes for RLC

- RLC packages typically use `@azure-rest/` naming convention
- The feature works with both script and command generation types
- TypeSpec emitter defaults to `@azure-tools/typespec-ts` for RLC
- Supports both swagger and TypeSpec-based RLC generation
