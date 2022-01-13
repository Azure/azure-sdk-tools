# Autorest Extension for Test Modeler

Generate \*.md config files in Azure REST API specification:

https://github.com/Azure/azure-rest-api-specs

## How to Generate Test Model

```
autorest --version=3.7.3 --use=<test model extension> --output-folder=<RP package path> --testmodeler.export-codemodel --clear-output-folder=false --debug <RP config md file path>
```

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

This options switch whether ExampleModel in generated in test model. While default to be true, it can be disabled like below:
```
testmodeler:
    use-example-model: false
```


## Autorest Pipeline Configurations

```yaml
clear-output-folder: false

try-require:
    - ./readme.test.md
    - ./readme.testmodeler.md

version: 3.7.3

use-extension:
  "@autorest/modelerfour" : "4.22.3"

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
