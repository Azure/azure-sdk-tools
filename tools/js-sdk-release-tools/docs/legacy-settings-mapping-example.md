# Example TypeSpec Configuration with Legacy Settings

This example shows how to use the Legacy Settings Mapping feature with old TypeSpec configuration files.

## Old Configuration (tspconfig.yaml)

```yaml
# Legacy configuration with old setting names
parameters:
  old-service-dir:
    default: "sdk/widgets"
  old-package-dir: 
    default: "arm-widgets"

options:
  "@azure-tools/typespec-ts":
    legacy-emitter-name: "old-emitter-value"
    package-name: "@azure/arm-widgets"
    is-modular-library: true
```

## Usage with Legacy Settings Mapping

### Command Line

```bash
# Enable legacy settings mapping via command line parameter
node autoGenerateInPipeline.js \
  --inputJsonPath input.json \
  --outputJsonPath output.json \
  --enableLegacySettingsMapping

# Or via environment variable
export ENABLE_LEGACY_SETTINGS_MAPPING=true
node autoGenerateInPipeline.js \
  --inputJsonPath input.json \
  --outputJsonPath output.json
```

### What Happens During Processing

When legacy settings mapping is enabled, the old configuration above gets automatically transformed to:

```yaml
# Effective configuration used internally
parameters:
  service-dir:
    default: "sdk/widgets"
  package-dir:
    default: "arm-widgets"

options:
  "@azure-tools/typespec-ts":
    "@azure-tools/typespec-ts": "old-emitter-value"
    package-name: "@azure/arm-widgets"
    is-modular-library: true
```

## Log Output

When the feature runs, you'll see log messages like:

```text
[INFO]: Applying legacy settings mapping...
[INFO]: Mapping legacy parameter 'old-service-dir' to 'service-dir'
[INFO]: Mapping legacy parameter 'old-package-dir' to 'package-dir'
[INFO]: Mapping legacy emitter option 'legacy-emitter-name' to '@azure-tools/typespec-ts' for emitter '@azure-tools/typespec-ts'
[INFO]: Legacy settings mapping completed.
```

## Migration Path

1. **Phase 1**: Enable legacy settings mapping to support both old and new configurations
2. **Phase 2**: Update your TypeSpec configurations to use the new setting names
3. **Phase 3**: Test thoroughly with the new configurations
4. **Phase 4**: Disable legacy settings mapping once migration is complete

## Notes

- The feature only maps settings when the new setting doesn't already exist
- Custom mapping rules can be added by modifying the `defaultLegacySettingsMapping` in the source code
- This feature is designed for both Modular Client and Rest Level Client SDK generation
- Both Modular Client (SDKType.ModularClient) and Rest Level Client (SDKType.RestLevelClient) pipelines support this feature
