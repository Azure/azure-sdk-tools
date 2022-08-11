# AppPlatform

> see https://aka.ms/autorest
> This is the AutoRest configuration file for AppPlatform.

```yaml
openapi-type: arm
azure-arm: true
require:
    - https://github.com/Azure/azure-rest-api-specs/blob/c943ce5e08690d4b0c840245a6f6f3ed28e56886/specification/appplatform/resource-manager/readme.md
clear-output-folder: true
tag: package-preview-2020-11
test-resources:
    - test: Microsoft.AppPlatform/preview/2020-11-01-preview/scenarios/Spring.yaml
testmodeler:
    api-scenario-loader-option:
        fileRoot: https://github.com/Azure/azure-rest-api-specs/blob/eb829ed4739fccb03dd2327b7762392e74c80ae4/specification/appplatform/resource-manager
        swaggerFilePaths:
          - 'Microsoft.AppPlatform/preview/2020-11-01-preview/appplatform.json'
```
