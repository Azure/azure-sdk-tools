---
name: typespec-new-project
description: |
  Initialize and bootstrap new TypeSpec projects for Azure services. USE FOR: onboard service to TypeSpec, create TypeSpec project, start ARM resource provider, set up data plane API, bootstrap TypeSpec for Azure SDK, initialize azure-core or azure-arm project, new TypeSpec definition.
  DO NOT USE FOR: converting existing swagger (use azsdk_convert_swagger_to_typespec), validating TypeSpec (use azsdk_run_typespec_validation).
---

# TypeSpec New Project Skill

Initialize a new TypeSpec project for Azure services.

## When to Use This Skill

- **Onboard** a new Azure service to TypeSpec
- **Create** or **initialize** a new TypeSpec project
- **Start** a new ARM resource provider definition
- **Set up** TypeSpec for a data plane API
- **Bootstrap** a TypeSpec project for Azure SDK generation

## Template Selection

| Template | Use When |
|----------|----------|
| `azure-arm` | ARM/Resource Manager services, Azure Portal resources, `Microsoft.*` namespaces |
| `azure-core` | Data plane services, client APIs, Azure AI, Storage data operations |

## Execution Flow

1. **Gather info**: Service namespace (Pascal case), service type (ARM/Data Plane), output directory
2. **Call tool**: `azsdk_typespec_init_project` with `outputDirectory`, `template`, `serviceNamespace`
3. **Post-init**: `npm ci`, edit `.tsp` files, `npx tsp compile .`

## Requirements

- Output directory must be under `azure-rest-api-specs/specification/`
- Directory must be empty or not exist
- Template: exactly `azure-arm` or `azure-core`

## Related Tools

| Tool | Use When |
|------|----------|
| `azsdk_typespec_init_project` | Execute the actual project initialization |
| `azsdk_convert_swagger_to_typespec` | Converting existing swagger to TypeSpec |
| `azsdk_run_typespec_validation` | Validating TypeSpec after editing |

See [references/detailed-guide.md](references/detailed-guide.md) for scenarios and troubleshooting

### "Invalid --output-directory, must be under azure-rest-api-specs"
- Ensure you've cloned the specs repo
- Provide the full absolute path to a directory under `/specification/`

### "Invalid --template"
- Use exactly `azure-arm` or `azure-core` (case-sensitive)

### "Directory not empty"
- The output directory must be empty
- Either clear it or choose a different path
