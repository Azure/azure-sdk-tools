---
name: typespec-new-project
description: |
  Initializes new TypeSpec projects for Azure services. TRIGGERS: create TypeSpec project, new TypeSpec, initialize TypeSpec, onboard service to TypeSpec, start ARM resource provider, set up data plane API, bootstrap TypeSpec, azure-arm project, azure-core project
---

# TypeSpec New Project

Initializes a new TypeSpec project for Azure SDK generation.

## When to Use

- Onboard a new Azure service to TypeSpec
- Create or initialize a new TypeSpec project
- Start a new ARM resource provider definition
- Set up TypeSpec for a data plane API

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

## Troubleshooting

See [references/detailed-guide.md](references/detailed-guide.md) for detailed scenarios.

**"Invalid --output-directory"**: Ensure path is under `azure-rest-api-specs/specification/`

**"Invalid --template"**: Use exactly `azure-arm` or `azure-core` (case-sensitive)

**"Directory not empty"**: Clear the directory or choose a different path
