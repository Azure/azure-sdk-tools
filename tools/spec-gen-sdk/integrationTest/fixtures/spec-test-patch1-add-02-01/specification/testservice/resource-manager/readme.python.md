# Python

```yaml !$(track2)
python:
  output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice
  payload-flattening-threshold: 2
  namespace: azure.mgmt.testservice
  package-name: azure-mgmt-testservice
  package-version: 1.0.1
  clear-output-folder: true
  output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/
```

```yaml $(track2)
output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice
payload-flattening-threshold: 2
namespace: azure.mgmt.testservice
package-name: azure-mgmt-testservice
package-version: 1.0.1
clear-output-folder: true
no-namespace-folders: true
```

```yaml $(multiapi) && $(track2)
batch:
  - tag: package-2020-01
  - tag: package-2020-02
  - multiapiscript: true
```

```yaml $(multiapiscript)
output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/
clear-output-folder: false
perform-load: false
```

``` yaml $(tag) == 'package-2020-01'
namespace: azure.mgmt.testservice.v2020_01
output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/v2020_01
python:
  namespace: azure.mgmt.testservice.v2020_01
  output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/v2020_01
```

``` yaml $(tag) == 'package-2020-02'
namespace: azure.mgmt.testservice.v2020_02
output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/v2020_02
python:
  namespace: azure.mgmt.testservice.v2020_02
  output-folder: $(python-sdks-folder)/testservice/azure-mgmt-testservice/azure/mgmt/testservice/v2020_02
```
