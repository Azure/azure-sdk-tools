## Overview

This application tokenises a Java project into a format useful for Java API reviews. It takes in one or more Maven-generated `sources.jar` files, and reads all files within it.

## Building

Compile to a fatjar using the following command: </br>`mvn clean package`

## Releasing

The existing APIView CI pipeline validates changes to this project. To publish
`com.azure:apiview-java-processor` to the Java dev feed, manually run `release.yml` from `main`
with **Publish to the Java dev feed** enabled.

Publishing a version is immutable. Update the version in `pom.xml` before releasing a new package.
The pipeline publishes only to the Azure Artifacts Java dev feed, not Maven Central.

## How To Use

Compile your source code using Maven `mvn clean package`. This will create a `target` directory containing
the built jar files, one of which will take the form `<library-name>-sources.jar`.

The application is run using the following structure:
</br>
`java -jar apiview-java-processor-1.0.0.jar <comma-separated list of jar files> <outputDirectory>` 

For example:</br>

* **One Jar File:** `java -jar apiview-java-processor-1.0.0.jar application-sources.jar temp`
* **Multiple Jar Files:** `java -jar apiview-java-processor-1.0.0.jar application-sources.jar,test-library-sources.jar,other-library-sources.jar temp`
