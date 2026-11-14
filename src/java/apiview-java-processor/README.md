<!-- Copyright (c) Microsoft Corporation. All rights reserved.
     Licensed under the MIT License. -->

# Java APIView Processor

The Java APIView processor generates API reviews from one or more Maven `-sources.jar` files.
Processor updates are imported from [Azure/azure-sdk-for-java#51226](https://github.com/Azure/azure-sdk-for-java/pull/51226).

## Building

From the repository root, build the executable JAR and run tests:

```shell
mvn -f src/java/apiview-java-processor/pom.xml clean verify
```

Artifacts are written to `src/java/apiview-java-processor/target`.
The standalone build does not require SDK-repository formatting or lint configuration.

## Usage

```text
java -jar apiview-java-processor-<version>.jar [--renderer=json|markdown] <comma-separated sources.jar files> <output-directory>
```

JSON is the default renderer:

```shell
java -jar apiview-java-processor-<version>.jar application-1.0.0-sources.jar output
```

Use Markdown for a fenced Java API snapshot without Javadoc:

```shell
java -jar apiview-java-processor-<version>.jar --renderer=markdown application-1.0.0-sources.jar output
```

Separate multiple input JAR paths with commas.
