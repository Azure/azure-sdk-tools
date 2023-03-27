# Autorest Extension for Test Modeler

Generate \*.md config files in Azure REST API specification:

https://github.com/Azure/azure-rest-api-specs

## How to Generate Test Model

```
autorest --version=3.7.3 --use=<test model extension> --output-folder=<RP package path> --testmodeler.export-codemodel --clear-output-folder=false --debug <RP config md file path>
```

## Contribution

The testmodeler use rush managing the projects. So if there is changes on package.json, remember to execute `rush update` updated.
~~~
> rush update
~~~

## Configurations

Below are options can be used for autorest.testmodeler

### --debug

Generate modeler files in [output-foler]/\_\_debuger for debug purpose.

### --testmodeler.mock.send-example-id

In generated mock test, send swagger example-id to mock service host in each request. This implicitly ask the mock service host to verify request body by swagger example content. Default true.

### --testmodeler.mock.verify-response

In generated mock test, response value will be verified with the example files. Default true.

### --testmodeler.mock.disabled-examples

This is an array parameter can be assigned in autorest readme file.
For instance, examples Extensions_Get and Extensions_Delete will not be used in mock test generation with below configuration.

```
testmodeler:
    mock:
        disabled-examples:
            - Extensions_Get
            - Extensions_Delete
```

### --testmodeler.scenario.variable-defaults.location

Set location used in scenario test, for instance:

```
testmodeler:
    scenario:
        variable-defaults:
            location: eastus
```

### --testmodeler.scenario.codemodel-restcall-only

If there is no 'test-resources' defined in readme file and the readme is located in local file system, testmodeler will search all available api-scenario and try to load them as test scenario.
In this context, option codemodel-restcall-only defined whether testmodeler abandon api-scenarios who contains restcall steps that can't be linked to an operation in codemodel.

The default value of this option is true.

### --testmodeler.use-example-model

This option switch whether ExampleModel in generated in test model. While default to be true, it can be disabled like below:
```
testmodeler:
    use-example-model: false
```

### --testmodeler.add-armtemplate-payload-string

This option switch whether StepArmTemplateModel.armTemplatePayloadString is added into testmodeler. While default to be false, it can be enabled like below:
```
testmodeler:
    add-armtemplate-payload-string: true
```

### --testmodeler.api-scenario-loader-option

The api-scenarios are loaded from the autorest input-files by default. This option provide a gate to load api-scenario from other remote/branch/commit.
This option are passed through directly to oav scenario loader, refer to https://github.com/Azure/oav/blob/develop/lib/apiScenario/apiScenarioLoader.ts#L60 for more detail of it. A sample for this option:
```
testmodeler:
    api-scenario-loader-option:
        fileRoot: https://github.com/Azure/azure-rest-api-specs/blob/eb829ed4739fccb03dd2327b7762392e74c80ae4/specification/appplatform/resource-manager
        swaggerFilePaths:
          - 'Microsoft.AppPlatform/preview/2020-11-01-preview/appplatform.json'
```

### --test-resources

The list of api-scenarios want to get loaded into testmodeler. Below is a sample:
```
test-resources:
    - test: Microsoft.AppPlatform/preview/2020-11-01-preview/scenarios/Spring.yaml
```

### --testmodeler.export-explicit-type

Whether to export codemodel with tags on primitive types, default as false. Demonstrate on the output values:
```
// with primitive types:
isDataAction: !!bool false
count: !!int 64


// with no types:
isDataAction: false
count: 64
```

### --testmodeler.explicit-types

A list for types need to explicitly tagged when export-explicit-type is true. The default explicitTypes are ['bool', 'int', 'float', 'timestamp']. Test generators can change it like below:

```
testmodeler:
    explicit-types:
        - bool
        - int
        - float
```

## Autorest Pipeline Configurations

```yaml
clear-output-folder: false

try-require:
    - ./readme.test.md
    - ./readme.testmodeler.md

version: 3.9.3

use-extension:
  "@autorest/modelerfour" : "4.25.0"

pipeline:
    test-modeler:
        input: modelerfour/identity
        output-artifact: source-file-test-modeler
    testmodeler/emitter:
        input: test-modeler
        scope: scope-testmodeler/emitter

scope-testmodeler/emitter:
    input-artifact:
        - source-file-test-modeler
    output-uri-expr: $key
    
testmodeler:
    split-parents-value: true
```

```yaml $(debug)
testmodeler:
    export-codemodel: true
```
