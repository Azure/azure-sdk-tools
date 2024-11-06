# Test Service

``` yaml
openapi-type: arm
tag: package-2020-01
```

``` yaml $(tag) == 'package-2020-01'
input-file:
- Microsoft.AnotherService/stable/2020-01-01/AnotherService.json
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
  namespace: anotherservice
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
  package-name: "@azure/another-service"
  output-folder: "$(typescript-sdks-folder)/sdk/anotherservice/arm-anotherservice"
  clear-output-folder: true
  generate-metadata: true
```

``` yaml $(python)
python:
  basic-setup-py: true
  output-folder: $(python-sdks-folder)/anotherservice/azure-mgmt-anotherservice
  azure-arm: true
  license-header: MICROSOFT_MIT_NO_VERSION
  payload-flattening-threshold: 2
  namespace: azure.mgmt.anotherservice
  package-name: azure-mgmt-anotherservice
  package-version: 1.0.0
  clear-output-folder: true
```

```yaml $(python) && $(multiapi)
batch:
  - tag: package-2020-01
```

``` yaml $(tag) == 'package-2020-01' && $(python)
python:
  namespace: azure.mgmt.anotherservice.v2020_01
  output-folder: $(python-sdks-folder)/anotherservice/azure-mgmt-anotherservice/azure/mgmt/anotherservice/v2020_01
```
