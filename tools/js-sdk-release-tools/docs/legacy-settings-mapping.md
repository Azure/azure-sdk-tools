# Legacy Settings Mapping Feature

This document describes the Legacy Settings Mapping feature, which allows old TypeSpec configuration settings to be automatically adapted to new settings for backwards compatibility.

## Overview

When upgrading TypeSpec projects or migrating from older configurations, some setting names or structures may have changed. The Legacy Settings Mapping feature provides a way to automatically map old settings to their new equivalents during package generation.

## Enabling the Feature

The Legacy Settings Mapping feature can be enabled in two ways:

### 1. Command Line Parameter

```bash
node autoGenerateInPipeline.js --enableLegacySettingsMapping
```

### 2. Environment Variable

```bash
export ENABLE_LEGACY_SETTINGS_MAPPING=true
node autoGenerateInPipeline.js
```

## How It Works

When enabled, the feature:

1. Resolves the TypeSpec configuration options as usual
2. Applies the legacy settings mapping before using the configuration
3. Maps old parameter names to new ones in both:
   - `configFile.parameters` section
   - Emitter-specific options

## Built-in TypeSpec Emitter Mappings

The following mappings are built into the tool and apply only to TypeSpec emitter options for `@azure-tools/typespec-ts`:

| Legacy Option | New Option | Description |
|---------------|------------|-------------|
| `generateTest` | `generate-test` | Controls test file generation |
| `packageDetails` | `package-details` | Package metadata configuration |
| `generateMetadata` | `generate-metadata` | Metadata generation settings |

## Internal Mapping Logic

The mapping logic is hardcoded within the `applyLegacySettingsMapping()` function and is not user-configurable. This ensures consistency and prevents configuration drift across different projects.

## Usage in Different SDK Types

- **Modular Client (SDKType.ModularClient)**: Full support with configurable mapping
- **Rest Level Client (SDKType.RestLevelClient)**: Full support with configurable mapping
- **High Level Client (SDKType.HighLevelClient)**: Not applicable

## Technical Implementation

The feature is implemented through:

1. `applyLegacySettingsMapping()` function in `src/common/utils.ts` with internal mapping
2. Updated `getGeneratedPackageDirectory()` function to use the legacy mapping
3. Enhanced `ModularClientPackageOptions` interface to include the mapping flag
4. Enhanced `generateRLCInPipeline()` function to support the mapping flag
5. Modified command line interface to accept the `--enableLegacySettingsMapping` parameter

## Mapping Scope

The legacy settings mapping only applies to TypeSpec emitter options within the `"@azure-tools/typespec-ts"` emitter configuration. It does not affect global TypeSpec settings or other emitter configurations.

## Example Configuration

Before mapping:

```yaml
# tspconfig.yaml
options:
  "@azure-tools/typespec-ts":
    api-version: "v1"
    generateTest: true
    generateMetadata: false
    packageDetails:
      name: "@azure/ai-agents"
    flavor: azure
```

After mapping (with built-in mappings):

```yaml
# tspconfig.yaml  
options:
  "@azure-tools/typespec-ts":
    api-version: "v1"
    generate-test: true
    generate-metadata: false
    package-details:
      name: "@azure/ai-agents"
    flavor: azure
```

## Key Features

- **TypeSpec Emitter Focused**: Only maps options within the `"@azure-tools/typespec-ts"` emitter configuration
- **Non-Destructive**: Won't overwrite existing new parameter names
- **Configurable**: Can be enabled via command-line parameter or environment variable
- **Transparent**: Comprehensive logging of all mapping operations
- **Built-in Mappings**: Uses predefined mappings for common legacy options

## Logging

When the feature is enabled, you'll see log messages indicating which mappings are being applied:

```text
Applying legacy settings mapping...
Mapping legacy emitter option 'generateTest' to 'generate-test' for emitter '@azure-tools/typespec-ts'
Mapping legacy emitter option 'packageDetails' to 'package-details' for emitter '@azure-tools/typespec-ts'
Legacy settings mapping completed.
```

## Migration Strategy

1. Enable the feature during the transition period
2. Update your TypeSpec configurations to use the new setting names
3. Test thoroughly with both old and new configurations
4. Disable the feature once migration is complete

## Troubleshooting

- If mappings aren't working, check that the feature is properly enabled
- Verify that the old setting names match exactly with the mapping configuration
- Check the logs for mapping activity
- Ensure that new settings don't already exist (mappings only apply when new settings are missing)
