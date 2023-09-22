# AppPlatform

> see https://aka.ms/autorest
> This is the AutoRest configuration file for AppPlatform.

```yaml
openapi-type: arm
azure-arm: true
require:
    - https://github.com/Azure/azure-rest-api-specs/blob/7572b87e95c992ac8b68db7783d3ac1a0d79010a/specification/compute/resource-manager/readme.md
clear-output-folder: true
tag: package-2021-11-01
modelerfour:
    lenient-model-deduplication: true
test-resources:
    - test: Microsoft.Compute/stable/2021-11-01/scenarios/vm_quickstart.yaml
    - Microsoft.Compute/stable/2021-11-01/scenarios/vm_quickstart_deps.yaml
testmodeler:
    scenario:
        codemodel-restcall-only: false
```
