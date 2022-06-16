# Release History

## 0.1.17 (2022-05-17)

### Features Added

* Added `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable to the `$ENV_FILE` in stress containers.

### Breaking Changes

### Bugs Fixed

* Remove duplicate values from `$ENV_FILE` if they would be set by the resource deployment scripts.

### Other Changes
