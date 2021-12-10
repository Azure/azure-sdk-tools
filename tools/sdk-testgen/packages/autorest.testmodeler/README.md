# Autorest Extension for Test Modeler

Generate \*.md config files in Azure REST API specification:

https://github.com/Azure/azure-rest-api-specs

## How to Generate Test Model

```
autorest --version=3.6.2 --use=<test model extension> --output-folder=<RP package path> --testmodeler.export-codemodel --clear-output-folder=false --debug <RP config md file path>
```

## Configurations

Below are options can be used for autorest.testmodeler

### --debug

Generate modeler files in [output-foler]/\_\_debuger for debug purpose.

### --testmodeler.mock.send-example-id

In generated mock test, send swagger example-id to mock service host in each request. This implicitly ask the mock service host to verify request body by swagger example content.

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

## Autorest Pipeline Configurations

```yaml
clear-output-folder: false

try-require:
    - ./readme.test.md
    - ./readme.testmodeler.md

version: 3.6.2

use-extension:
  "@autorest/modelerfour" : "4.21.1"

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
