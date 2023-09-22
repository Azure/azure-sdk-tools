# SDK Automation Customization

This is the specification of the SDK Automation customization configuration.

## SDK Automation workflow

### Run SDK Generation

SDK Automation is launched in azure pipeline. It runs tasks in the following steps:

1. Get `codegen_to_sdk_config.json` from cloned SDK repository. For the definition of the config see [CodegenToSdkConfig](#codegentosdkconfig).

2. Launch __initTask__ defined in [CodegenToSdkConfig](#codegentosdkconfig). All the script's working directory is root folder of cloned SDK repository.

3. Launch __generateAndBuildTask__ defined in [CodegenToSdkConfig](#codegentosdkconfig) with [generateAndBuildInput.json](#generateandbuildinput). The script should produce [generateAndBuildOutput.json](#generateandbuildoutput). Then the [generateAndBuildOutput.json](#generateandbuildoutput) will be parsed and the generated codes and artifacts will be stored in storage account.

4. Launch __mockTestTask__ to run mock test with [mockTestInput.json](#mocktestinput). The script should produce [mockTestOutput.json](#mocktestoutput). Then the [mockTestOutput.json](#mocktestoutput) will be parsed and the test result will be stored in database.

## Definitions

### CodegenToSdkConfig
This is type of file `./codegen_to_sdk_config.json` in sdk repo.
The running environment of these scripts would be expected to be __Ubuntu 20.04__ on Azure Pipeline. This may change in the future. All the running script should be executable.
The working folder of all the scripts is the __root folder of sdk repo__.

#### CodegenToSdkConfig Example
``` jsonc
{
  "init": {
      "initScript": {
          // Script to init dependencies.
          // Param: <path_to_initOutput.json>
          // initOutput.json: See #initOutput
        "path": ".scripts/sdk_init.sh"
      }
    },
  "generateAndBuild": {
    // Param: <path_to_generateAndBuildInput.json> <path_to_generateAndBuildOutput.json>
    // path_to_generateAndBuildInput.json: See #GenerateAndBuildInput.
    // path_to_generateAndBuildOutput.json: See #GenerateAndBuildOutput.
      "generateAndBuildScript": {
        "path": ".scripts/sdk_generateAndBuild.sh",
        "logFilter": {
           // filter for error msg and warning msg.
           "error": "(error|failed|exception)",
           "warning": "warn"
         }
      }
  },
  "mockTest": {
    "mockTestScript": {
      "path": ".scripts/sdk_mockTest.sh"
    }
  }
}

```

#### CodegenToSdkConfig Schema
See [CodegenToSdkConfigSchema.json](schema/CodegenToSdkConfigSchema.json).

### GenerateAndBuildInput

Input file for generate and build script.

#### GenerateAndBuildInput Example

```jsonc
{
  "specFolder": "/z/work/azure-rest-api-specs/specification",
  "headSha": "fce3400431eff281bddd04bed9727e63765b8da0",
  "headRef": "refs/pull/1234/merge",
  "repoHttpsUrl": "https://github.com/Azure/azure-rest-api-specs.git",
  "relatedReadmeMdFile": "compute/resource-manager/readme.md"
  "serviceType": "resource-manager",
  "skipGeneration": "false"
}
```

#### GenerateAndBuildInput Schema

See [./GenerateAndBuildInputSchema.json](schema/GenerateAndBuildInputSchema.json)

### GenerateAndBuildOutput

Output file for generate script.

#### GenerateAndBuildOutput Example

```jsonc
{
  "packages": [
    {
      "packageName": "@azure/arm-compute",
      "result": "succeeded",
      "path": [
        "sdk/compute/arm-compute",
        "rush.json"
      ],
      "packageFolder": [
        "sdk/compute"
      ],
      "changelog": {
        "content": "Feature: something \n Breaking Changes: something\n",
        "hasBreakingChange": true
      },
      "artifacts": [
        "sdk/compute/azure-arm-compute-1.0.0.tgz",
      ]
    }
  ]
}
```

#### GenerateAndBuildOutput Schema

See [./GenerateAndBuildOutputSchema.json](schema/GenerateAndBuildOutputSchema.json)

### InitOutput

#### InitOutput Schema

```jsonc
{
  "type": "object",
  "properties": {
    "envs": {
      // Environment variable to be set in following scripts. Not Implement
      "additionalProperties": {
        "type": "string"
      }
    }
  }
}
```
