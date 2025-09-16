# Internal Java API Diff Tool

Semantic Java source diff used internally by the TypeSpec client update pipeline.

## Purpose

Parses two Java source trees and emits a JSON array of ApiChange objects consumed by `JavaUpdateLanguageService`.

Currently detects:

- MethodAdded
- MethodRemoved
- MethodSignatureChanged (parameter name changes only)

## Build

```bash
mvn -q package
```

The shaded jar will be at `target/apidiff-<version>-jar-with-dependencies.jar`.
Copy or rename to:

```text
<azsdk bin>/tools/java/apidiff/apidiff.jar
```

Or set `APIDIFF_JAR` environment variable to the absolute path of the shaded jar.

## Invocation

```bash
java -jar apidiff.jar --old <oldSourceRoot> --new <newSourceRoot> --format json
```

Outputs JSON (pretty printed) like:

```json
[
  {
    "kind": "MethodAdded",
    "symbol": "com.example.Client#newOp()",
    "detail": "added"
  }
]
```

## Internal Auto-Build

If the jar is missing at runtime, the .NET service will attempt a one-time `mvn -q -DskipTests package` using either:

- `Tools/APIDiffTool/pom.xml`

If build succeeds the shaded jar is copied to the conventional path.

## Extending

Future enhancements may include:

- Parameter type / return type changes
- Class/interface additions/removals
- Field changes
- Visibility / modifier changes

Keep JSON contract stable to avoid breaking the CLI consumer.
