# Release History

## 0.3.1 (2024-01-17)

### Features Added

Add `GIT_COMMIT` to container environment and `gitCommit` to job/pod labels. The value corresponds to the git commit of the stress test being deployed if it's in a git repository.

## 0.3.0 (2023-09-22)

### Breaking Changes

Move parallel job configuration into special matrix field `parallel` so that
parallelism can be set per scenario. Remove parallel-deploy-job-template way
of setting parallelism added in the 0.2.1 release.

### Features Added

Adds support for pod disruption budgets when helm values PodDisruptionBudgetExpiry and PodDisruptionBudgetExpiryCron are set. When the expiry is set, a pdb will be created matching all pods in a release, and a cron job will be created to clean up the pdb on a specified date. This allows users to mark a test as non-interruptable so that kubernetes will not shut down the node for upgrades, rebalancing, etc.

## 0.2.2 (2023-09-21)

### Features Added

Added role binding for all pods. Allowing pods to have read access to information of other pods under the same namespace.

### Bugs Fixed

Fixed racing condition for ARM deployment when multiple pods run in parallel.

## 0.2.1 (2023-09-14)

### Features Added

Added template for running multiple pods in parallel - `"stress-test-addons.parallel-deploy-job-template.from-pod"`

## 0.2.0 (2022-10-21)

### Features Added

### Breaking Changes

Switched to scenarios matrix for setting the configuration for stress tests. Users should now use the matrix generation for configuring the test name, image, image build directory, test targets and other custom configuration. Users can can reference those values in the chart files with `{{ .Stress.<value_key> }}`.

### Bugs Fixed

### Other Changes

## 0.1.20 (2022-07-25)

### Features Added

### Breaking Changes

### Bugs Fixed

Fixed a bug introduced in 0.1.18 with `{{ .Stress.BaseName }}` not being deterministic. This meant that chaos policies using `{{ .Stress.BaseName }}` to block DNS entries did not use the same string value as the test template resources. The new BaseName is generated via a sha1 hash of the resource group name (which includes the scenario name, release name and release revision), truncated to length 5, and prefixed with a `s` character for maximum azure resource naming compatibility.

### Other Changes

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
