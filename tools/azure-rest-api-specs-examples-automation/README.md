# Automation for SDK Examples

A pipeline tool for collecting SDK examples for [azure-rest-api-specs-examples](https://github.com/Azure/azure-rest-api-specs-examples).

Entry point is `automation/main.sh`.

Configuration file is `automation/configuration.json`.

# Workflow

For each language:
1. List recent releases from each github repository.
2. For management-plane SDK, checkout the SDK repository on the specific release tag.
3. Collect sample codes from the SDK repository.
4. Validate the package and code.
5. Create PR to "azure-rest-api-specs-examples" repository.

After process completed, create PR to "azure-rest-api-specs-examples" "metadata" branch for csv files.
