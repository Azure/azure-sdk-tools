# Python

```yaml !$(track2)
python:
  payload-flattening-threshold: 2
  namespace: azure.mgmt.testservice
  package-name: azure-mgmt-testservice
  package-version: 1.0.0
  clear-output-folder: true
  no-namespace-folders: true
  output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/
```

```yaml $(track2)
payload-flattening-threshold: 2
namespace: azure.mgmt.testservice
package-name: azure-mgmt-testservice
package-version: 1.0.0
clear-output-folder: true
no-namespace-folders: true
```

```yaml $(multiapi) && $(track2)
batch:
  - tag: package-2020-01
  - multiapiscript: true
```

```yaml $(multiapiscript)
output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/
clear-output-folder: false
perform-load: false
```

``` yaml $(tag) == 'package-2020-01' && $(track2)
namespace: azure.mgmt.testservice.v2020_01
output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/v2020_01
```
