Simple helm plugin to enable the file protocol for `helm repo add file://` paths in order to simplify
local development of the stress-test-addon library chart with stress test charts that take it as a dependency.
Requires powershell core.

Example usage to override remove chart dependencies with a local version below.

For a helm chart with a named chart dependency `@stress-test-charts`:

```
$ tail Chart.yaml -n 3

- name: stress-test-addons
  version: ~0.3.0
  repository: "@stress-test-charts"
```

Install named `stress-test-charts` repository from remote ur:

```
helm repo add stress-test-charts https://azuresdkartifacts.z5.web.core.windows.net/stress/
```

Install remote library chart from stress-test-charts repository:

```
helm dependency update
```

Add file plugin to support `file://` repositories, this only has to be done once:

```
helm plugin add <git root>/tools/stress-cluster/cluster/kubernetes/stress-test-addons/file-plugin
```

Use local version of named `stress-test-charts` library chart:

```
helm repo add --force-update stress-test-charts file:///<git root>/azure-sdk-tools/tools/stress-cluster/cluster/kubernetes/stress-test-addons
helm dependency update
```

Revert to remote version of library chart

```
helm repo add --force-update stress-test-charts https://azuresdkartifacts.z5.web.core.windows.net/stress/
helm dependency update
```
