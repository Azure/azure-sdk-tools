# Test Service

``` yaml
openapi-type: arm
tag: package-2020-01
license-header: MICROSOFT_MIT_NO_VERSION
```

``` yaml $(tag) == 'package-2020-01'
input-file:
- Microsoft.TestService/stable/2020-01-01/TestService.json
```

``` yaml $(multiapi) && !$(track2)
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
  - repo: azure-sdk-for-python-track2
  - repo: azure-resource-manager-schemas
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


``` yaml $(cli)
cli:
  cli-name: TestService
  azure-arm: true
  license-header: MICROSOFT_MIT_NO_VERSION
  payload-flattening-threshold: 2
  namespace: azure.mgmt.TestService
  package-name: azure-mgmt-TestService
  clear-output-folder: false
```

``` yaml $(trenton)
trenton:
  cli_name: TestService
  azure_arm: true
  license_header: MICROSOFT_MIT_NO_VERSION
  payload_flattening_threshold: 2
  namespace: azure.mgmt.TestService
  package_name: azure-mgmt-TestService
  clear_output_folder: false
```
