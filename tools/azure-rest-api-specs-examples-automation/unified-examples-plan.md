# Unified example collection plan

## Goal

Support unified YAML example references in all five workers while retaining existing Swagger and TypeSpec JSON references. Resolve the corresponding JSON example in the specs checkout; collection does not generate JSON or run AutoRest.

Unified references have this form:

```text
<project-relative YAML path>#<explicit example id>
```

Examples:

```text
examples/SchemaRegistry.yaml#GetSchemaById
Compute/examples/VirtualMachines.yaml#virtualMachineExamples/Foo
```

The fragment is an extension-free relative JSON path. Preserve nested directories. Do not include an API version in the authored ID.

## Current implementation

`directory/examples_dir.py::try_find_resource_manager_example` is called by Java, Python, Go, JS/TS, and .NET. Each worker first splits the original reference into directory and filename.

For TypeSpec references, the helper reads `tsp-location.yaml`, searches for an authored TypeSpec JSON example, then searches for its resource-manager Swagger counterpart. Missing TypeSpec JSON currently raises an error before Swagger lookup.

Unified migrations delete the authored JSON copies. Split the YAML reference at `#` before any filesystem directory/filename split, and look for the emitted Swagger JSON directly. For legacy TypeSpec JSON references, also attempt Swagger lookup when the authored JSON is missing. Report not found if neither lookup succeeds.

## SDK API-version metadata

Paths are relative to the SDK package directory:

| Worker | Metadata file |
| --- | --- |
| Java | `src/main/resources/META-INF/*_metadata.json` |
| Python | `_metadata.json` |
| Go | `testdata/_metadata.json` |
| JS/TS | `metadata.json` |
| .NET | `metadata.json` |

All five were verified against current Compute packages and expose an `apiVersions` object:

```json
{
  "apiVersions": {
    "Compute": "2026-04-01",
    "ComputeDisk": "2026-03-02",
    "ComputeGallery": "2026-03-03",
    "ComputeSku": "2021-07-01"
  }
}
```

Read metadata from the checked-out SDK release, not current SDK main. Keep version selection out of the authored examples.

Both metadata and example resolution originate from TCGC's service/version map. Metadata keys use the fully qualified service namespace; multi-service YAML directories use its simple name. These match directly for Compute, but not necessarily for nested namespaces.

## Resolution logic

1. Normalize reference separators. Leave existing absolute specs-relative Swagger references on the existing path.
2. Recognize YAML `#id` references separately from legacy JSON references.
3. Use `tsp-location.yaml`'s `directory` to find the TypeSpec project root. Resolve the YAML path relative to it, retaining the service prefix.
4. Read the YAML's `$namespace` to match the full `apiVersions` key. For multiple services, also retain the service-directory scope. Use a single metadata entry when unambiguous; never guess the latest version or pick the first service. Reject conflicting or unresolved associations. A missing `$namespace` needs an unambiguous directory-to-metadata match or a single-entry map.
5. Append `.json` to the explicit ID, preserving its nested directories. Validate it as a relative path: no absolute paths or parent traversal.
6. Find the emitted Swagger example at the selected version and relative path, under the project/spec service scope. Support `stable/<version>/examples/...` and `preview/<version>/examples/...`. An aggregate such as Compute can emit Swagger at the shared project root rather than inside the individual subservice folder.
7. Prefer a unique scoped match. If multiple matches remain, use the service's Swagger linkage to disambiguate, or report ambiguity; do not silently select the first file. Do not scan unrelated services for a matching basename.
8. Return the resolved JSON directory and filename. Workers retain their existing sample-content handling and map `examples` to `examples-java`, `examples-python`, `examples-go`, `examples-js`/`examples-ts`, or `examples-dotnet`.

The resolver must not require an authored TypeSpec JSON copy for unified references. YAML references without an explicit ID do not provide this contract; report a clear unsupported/missing identity rather than inventing a filename.

For legacy JSON references, retain successful existing resolution and fallback behavior. If the authored copy is absent, use the version and nested path already present in the reference to try Swagger directly.

## Implementation

Implementation branch: `feature/unified-example-collection`.

1. Add complete-reference resolution in the shared directory module, returning directory and filename. Keep metadata discovery separate from path matching; use known metadata locations, not an unrestricted recursive JSON scan.
2. Update all five worker call sites to resolve before splitting. Preserve existing legacy APIs where needed.
3. Add focused shared resolver and worker regressions. Add the directory tests to CI if they are not otherwise discovered.
4. Run directory and affected worker test suites, then review the diff for compatibility. No commits or PRs are requested yet.

## Regression cases

- Schema Registry: `examples/SchemaRegistry.yaml#GetSchemaById`, with the same ID in three versions and metadata selecting the correct one. Authored JSON is absent.
- Compute: a shared project root, `Compute` and `ComputeDisk` folders, different selected versions, nested IDs, and same-named examples in other services/versions.
- Fully qualified namespace metadata keys versus simple directory names.
- Metadata loading from each of the five locations.
- Existing Swagger references, existing TypeSpec JSON references, and missing TypeSpec JSON with a valid Swagger counterpart.
- Missing metadata, ambiguous service/version/file matches, unsafe IDs, and missing Swagger JSON.
- Worker output filenames and nested destination directories remain correct.

## References and limits

- https://github.com/Azure/typespec-azure/issues/5774
- https://github.com/Azure/typespec-azure/pull/5835
- https://github.com/Azure/azure-rest-api-specs/pull/47494

Migration currently loses nested directories through `basename(examplePath)`. This collector cannot recover missing authored information; it can preserve a correctly authored nested ID. Legacy expansion's nested-directory creation is also outside this change.

Release collection remains management-plane focused. Schema Registry is a useful data-plane layout fixture, not a request to expand the release filters.
