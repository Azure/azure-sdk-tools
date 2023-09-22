Simple helm plugin to enable the file protocol for `helm repo add file://` paths in order to simplify
local development of the stress-test-addon library chart with stress test charts that take it as a dependency.
Requires powershell core.

Example usage to override remove chart dependencies with a local version below.

For a helm chart with a named chart dependency `@stresstestcharts`:

```
$ tail Chart.yaml -n 3

- name: stress-test-addons
  version: 0.1.16
  repository: "@stresstestcharts"
```

Install named `stresstestcharts` repository from remote ur:

```
helm repo add stresstestcharts https://stresstestcharts.blob.core.windows.net/helm/
```

Install remote library chart from stresstestcharts repositor:

```
helm dependency update
```

Add file plugin to support `file://` repositories, this only has to be done once:

```
helm plugin add <git root>/tools/stress-cluster/cluster/kubernetes/stress-test-addons/file-plugin
```

Use local version of named `stresstestcharts` library chart:

```
helm repo add --force-update stresstestcharts file:///<git root>/azure-sdk-tools/tools/stress-cluster/cluster/kubernetes/stress-test-addons
helm dependency update
```

Revert to remote version of library chart

```
helm repo add --force-update stresstestcharts https://stresstestcharts.blob.core.windows.net/helm/
helm dependency update
```
