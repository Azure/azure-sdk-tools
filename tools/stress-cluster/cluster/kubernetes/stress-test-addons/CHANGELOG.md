# Release History

## 0.1.19 (2022-06-30)

### Features Added

### Breaking Changes

### Bugs Fixed

### Other Changes

* Updated values.yaml environment configs to point to new playground cluster and remove test cluster

## 0.1.18 (2022-06-28)

### Features Added

* Added a 6 character random string `{{ .Stress.BaseName }}` that can be used for naming that doesn't break
  validation for resources like service bus, storage, etc. Bicep templates can consume this by adding a `BaseName`
  parameter.
* Inject `BASE_NAME` environment variable to deployment init container.
* Add resourceGroupName and baseName labels to job template.

### Breaking Changes

### Bugs Fixed

### Other Changes

## 0.1.17 (2022-05-17)

### Features Added

* Added `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable to the `$ENV_FILE` in stress containers.

### Breaking Changes

### Bugs Fixed

* Remove duplicate values from `$ENV_FILE` if they would be set by the resource deployment scripts.

### Other Changes
