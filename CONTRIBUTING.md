# Azure SDK Tools Contribution Guidelines

This is the repository used to host tools and libraries used by Azure SDK team engineering system. 
We allow to create tools in any language suitable for the tools.
For every newly developing tool, create a new folder with brief description on its name. 
Do not add third party tools directly in your folder, use it as dependencies.

## Coding Guidelines

See language specific guidelines:

- [PowerShell](https://github.com/Azure/azure-sdk-tools/blob/main/doc/development/powershell.md)

## Codeowners

Add code owner in [CODEOWNERS](https://github.com/Azure/azure-sdk-tools/blob/main/.github/CODEOWNERS) with following format:
```
/tools/<tool-name>/ @owner1 @owner2
```

## README

1. Add README.md file for every tool to illustrate:
* The purpose of the tool.
* Prerequisites before use.
* How to use, test and maintain the tool locally and remotely. 
* Better to include where the tool is being used.
* Example: [README.md](https://github.com/Azure/azure-sdk-tools/blob/main/tools/http-fault-injector/README.md) 

2. Add tool details to the index in root [README.md](https://github.com/Azure/azure-sdk-tools/blob/main/README.md#index).


## Testing

Provide certain test cases to cover important workflow, especially on how it gets used in azure pipelines or running in prod.

Example: [Test library](https://github.com/Azure/azure-sdk-tools/tree/main/tools/pipeline-witness/Azure.Sdk.Tools.PipelineWitness.Tests) 

If there is any bundle script, do provide end-to-end test on script as well.

Example: [Custom Test on ci.yml](https://github.com/Azure/azure-sdk-tools/blob/main/tools/code-owners-parser/ci.yml#L35)

## Release ci.yml

- For the tool which is publishing to public repository, do provide ci.yml for building, testing and releasing. 

Example: [ci.yml](https://github.com/Azure/azure-sdk-tools/blob/main/tools/CreateRuleFabricBot/ci.yml)

- Pipelines should use common templates from `eng/pipelines/templates`.

The naming convention of the pipelines: `tools - <tool-name> - ci` for the public builds and `tools - <tool-name>` for internal builds.

- Use internal builds for releasing steps, and conditioning those steps similar to https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/templates/stages/archetype-sdk-tool-dotnet.yml#L89. *TODO: Will define release template for tools in other languages*
