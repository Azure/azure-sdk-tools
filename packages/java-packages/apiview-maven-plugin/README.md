# Java APIView Maven Plugin

Generates `api.md` and `api.metadata.yml` from a built Java sources JAR. No generation scripts, plugin configuration, or checked-in API snapshots are required in the SDK repository.

## Generate a bundle

From a library module, run its normal build followed by the plugin:

```shell
mvn package com.azure.tools:apiview-maven-plugin:1.0.0-beta.1:generate
```

The package build must produce a `-sources.jar`. To reuse a previously built JAR:

```shell
mvn com.azure.tools:apiview-maven-plugin:1.0.0-beta.1:generate -Dapiview.sourcesJar=/path/to/library-1.0.0-sources.jar
```

Resolve released versions through the Azure SDK Java dev feed using the SDK repository's existing Maven settings.

| Property | Default |
| --- | --- |
| `apiview.sourcesJar` | Attached sources artifact, otherwise `target/${project.build.finalName}-sources.jar` |
| `apiview.outputDirectory` | `${project.build.directory}/api-review` |
| `apiview.packageRelativePath` | Module path relative to the nearest ancestor containing `.git` |
| `apiview.repositoryRoot` | Optional root override for exported sources without Git metadata |
| `apiview.skip` | `false`; aggregator POMs are skipped automatically |

For an explicit module path, including sparse or exported checkouts:

```shell
mvn com.azure.tools:apiview-maven-plugin:1.0.0-beta.1:generate -Dapiview.packageRelativePath=sdk/serialization/azure-json
```

Metadata records the literal package POM version and the forward-slash repository-relative path. `apiHash` is lowercase SHA-256 of the complete UTF-8, no-BOM, LF-normalized Markdown with one trailing newline. Existing bundles are preserved if generation or replacement fails.

## Develop

Use Maven 3.6.3+ and JDK 8+:

```shell
mvn -f packages/java-packages/apiview-maven-plugin/pom.xml clean verify
```

The plugin includes the shared processor sources and resources from `src/java/apiview-java-processor`; there is no separately published processor dependency. Unit tests and a Maven invocation test run during `verify`. CI also verifies the standalone processor.

## Release

Run `release.yml` manually from `main` in the internal project, opting into `publishDevFeed`. It builds and tests on Linux, then ESRP-signs all three JARs and GPG-signs/publishes the plugin JAR, POM, sources, and Javadoc to the Java dev feed on Windows. Existing versions cannot be overwritten.
Supply the ESRP service connection, identity, vault, and certificate parameters from the EngSys signing configuration. Register `ci.yml` as `tools - apiview-maven-plugin - ci` and `release.yml` as an internal release pipeline.
