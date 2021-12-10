# Azure SDK Tools Contribution Guidelines

This is the repository used to host tools and libraries used by Azure SDK team engineering system. 
We allow to create tools in any language suitable for the tools.
For every newly developing tool, create a new folder with name of brief description of the tool. 
Do not add third party tools directly in your folder, use it as dependencies.

## Codeowners

Add code owner per tool in [CODEOWNER](https://github.com/Azure/azure-sdk-tools/blob/main/.github/CODEOWNERS) with following format:
```
/tools/<tool-name>/ @owner1 @owner2
```

## README

Please add README.md file for every tool to illustrate:
* The purpose of the tool.
* Prerequisites before use
* How to use, test and maintain the tool locally and remotely. 
* Better to include where the tool is being used.

Example: [README](https://github.com/Azure/azure-sdk-tools/blob/main/tools/http-fault-injector/README.md) 

## Testing

Please provide certain test cases to cover important workflow, especially the tool using in azure pipelines or running in prod.

Example: [Test library](https://github.com/Azure/azure-sdk-tools/tree/main/tools/pipeline-witness/Azure.Sdk.Tools.PipelineWitness.Tests) 

If there is any bundle script, please provide end-to-end test on script as well.

Example: [Custom Test on ci.yml](https://github.com/Azure/azure-sdk-tools/blob/main/tools/code-owners-parser/ci.yml#L35)

## Release ci.yml

- For the tool which is publishing to public repository, please provide ci.yml for building, testing and releasing. 

Example: [ci.yml](https://github.com/Azure/azure-sdk-tools/blob/main/tools/CreateRuleFabricBot/ci.yml)

- Pipelines should use common templates from `eng/pipelines/templates`.

The naming convention of the pipelines: `tools - <tool-name> - ci` for the public builds and `tools - <tool-name>` for internal builds.

- Use internal builds for releasing steps, and conditioning those steps similar to https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/templates/stages/archetype-sdk-tool-dotnet.yml#L89. *TODO: Will define release template for tools in other languages*
