# Test Service

``` yaml
openapi-type: arm
tag: package-2020-01
```

``` yaml $(tag) == 'package-2020-01'
input-file:
- Microsoft.TestService/stable/2020-01-01/TestService.json
```

``` yaml $(multiapi)
batch:
  - tag: package-2020-01
```

``` yaml $(swagger-to-sdk)
swagger-to-sdk:
  - repo: azure-sdk-for-go
  - repo: azure-sdk-for-js
  - repo: azure-sdk-for-java
  - repo: azure-sdk-for-python
  - repo: azure-sdk-for-net
  - repo: azure-sdk-for-trenton
  - repo: azure-cli-extensions
```

``` yaml $(go)
go:
  license-header: MICROSOFT_APACHE_NO_VERSION
  namespace: testservice
  clear-output-folder: true
```

``` yaml $(tag) == 'package-2020-01' && $(go)
output-folder: $(go-sdk-folder)/services/$(namespace)/mgmt/2020-01-01/$(namespace)
```

``` yaml $(typescript)
typescript:
  azure-arm: true
  license-header: MICROSOFT_MIT_NO_VERSION
  payload-flattening-threshold: 2
  package-name: "@azure/test-service"
  output-folder: "$(typescript-sdks-folder)/sdk/testservice/arm-testservice"
  clear-output-folder: true
  generate-metadata: true
```

``` yaml $(python)
python:
  basic-setup-py: true
  output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice
  azure-arm: true
  license-header: MICROSOFT_MIT_NO_VERSION
  payload-flattening-threshold: 2
  namespace: azure.mgmt.testservice
  package-name: azure-mgmt-testservice
  package-version: 1.0.0
  clear-output-folder: true
```

```yaml $(python) && $(multiapi)
batch:
  - tag: package-2020-01
```

``` yaml $(tag) == 'package-2020-01' && $(python)
python:
  namespace: azure.mgmt.testservice.v2020_01
  output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/v2020_01
```
