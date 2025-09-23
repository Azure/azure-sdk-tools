## Overview

This application tokenises a Java project into a format useful for Java API reviews. It takes in one or more Maven-generated `sources.jar` files, and reads all files within it.

## Building

Compile to a fatjar using the following command: </br>`mvn clean package`

## How To Use

Compile your source code using Maven `mvn clean package`. This will create a `target` directory containing
the built jar files, one of which will take the form `<library-name>-sources.jar`.

The application is run using the following structure:
</br>
`java -jar apiview-java-processor-1.0.0.jar <comma-separated list of jar files> <outputDirectory>` 

For example:</br>

* **One Jar File:** `java -jar apiview-java-processor-1.0.0.jar application-sources.jar temp`
* **Multiple Jar Files:** `java -jar apiview-java-processor-1.0.0.jar application-sources.jar,test-library-sources.jar,other-library-sources.jar temp`

## Diff Mode (Experimental)

You can generate a semantic diff of two versions (sets) of sources using the integrated `--diff` mode. This analyses
public and protected classes, fields, methods and constructors and emits structured change objects.

### Command

```bash
java -jar apiview-java-processor-<version>.jar --diff \
  --old <comma-separated old source roots or -sources.jar paths> \
  --new <comma-separated new source roots or -sources.jar paths> \
  --out <outputDirectory>
```

Examples:

```bash
# Compare two sources jars
java -jar apiview-java-processor-1.33.0.jar --diff --old sdk-core-1.0.0-sources.jar --new sdk-core-1.1.0-sources.jar --out diff-output

# Compare multiple modules (directories) at once
java -jar apiview-java-processor-1.33.0.jar --diff --old ../old/sdk/core,../old/sdk/storage \
  --new ../new/sdk/core,../new/sdk/storage --out diff-output
```

### Output

The diff is written to `<outputDirectory>/apiview-diff.json` with schema version `1.0.0`.

High-level JSON structure:

```json
{
  "schemaVersion": "1.0.0",
  "changes": [
    {
      "changeType": "AddedMethod",
      "after": "public String getName()",
      "impact": "NonBreaking",
      "category": "Method",
      "meta": {
        "symbolKind": "Method",
        "fqn": "com.example.Foo",
        "methodName": "getName",
        "signature": "com.example.Foo#getName()",
        "visibility": "public",
        "returnType": "String",
        "parameterTypes": [],
        "parameterNames": []
      }
    }
  ]
}
```

`changeType` values (current set):
- AddedClass / RemovedClass
- AddedMethod / RemovedMethod / AddedOverload / RemovedOverload
- ModifiedMethodReturnType / ModifiedMethodDeprecation / ModifiedMethodVisibility / ModifiedMethodParameterNames
- AddedField / RemovedField / ModifiedFieldType / ModifiedFieldDeprecation

`impact` is a heuristic ("Breaking" or "NonBreaking"). `category` groups similar changes (e.g. ReturnType, Parameters, Overload, Field).

`meta.paramNameChange` is `true` for parameter name-only adjustments.

### Notes & Limitations
* Only public and protected members are considered.
* Parameter type comparison is based on erased (raw) types (generics stripped) in this first version.
* Nested types are processed; annotation types currently skipped.
* Visibility of classes is inferred from modifiers; package-private types are excluded.
* Additional categories (e.g., inheritance changes) may be added in future schema versions.

### Exit Codes
Diff mode exits with non-zero only on invalid arguments or internal errors (not on presence of breaking changes).


### Formatting

Output JSON is compact. Use external tools (e.g. `jq`, IDE formatters) if pretty-printing is desired.

