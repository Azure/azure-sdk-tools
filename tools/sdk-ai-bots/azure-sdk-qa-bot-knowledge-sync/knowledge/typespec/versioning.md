# TypeSpec Versioning

Common issues and solutions for API versioning and avoiding breaking changes in TypeSpec.

## @added decorator marks when a model, operation, or property was introduced

`@added(<version>)` from `@typespec/versioning` declares that an element was introduced at a specific API version and is present in that version and all later ones. TypeSpec uses this to reconstruct the API surface at each version when emitting.

```typespec
@added(Versions.v2024_02_01)
@route("/users/profile")
op createUserProfile(@body profile: { profileName: string }): void;
```

Related decorators: `@removed(<version>)` for removals, `@renamedFrom(<version>, <oldName>)` for renames, `@typeChangedFrom(<version>, <oldType>)` for type changes, `@madeOptional(<version>)` for required→optional changes.

Note: Versioning decorators themselves cannot be versioned. The types they annotate generally need to be versioned independently.

## @useDependency is required in client.tsp when outside the versioned service namespace

If your `client.tsp` customization namespace is not a child of the versioned service namespace, you must use `@useDependency` to bind it to specific versions. Without it, the compiler cannot resolve versioned shapes and emits a "referencing a versioned namespace" diagnostic.

Create a mirrored versions enum in `client.tsp` and bind each version:

```typespec
@versioned(Versions)
namespace ContosoClient {
  using Contoso.WidgetManager;

  enum Versions {
    @useDependency(Azure.Core.Versions.v1_0_Preview_2)
    @useDependency(Contoso.WidgetManager.Versions.v2022_08_30)
    v2022_08_30: "2022-08-30",

    @useDependency(Azure.Core.Versions.v1_0_Preview_2)
    @useDependency(Contoso.WidgetManager.Versions.v2025_01_30)
    v2025_01_30: "2025-01-30",
  }
}
```

If `client.tsp` is a child namespace of the versioned service namespace, no explicit `@useDependency` is needed. Version-specific customizations use the same `@added`/`@removed` patterns as service code.

## Model validation failures when adding new parent models in versioned TypeSpec APIs

When migrating or evolving TypeSpec APIs, a common validation failure occurs after introducing new parent or related models in a newer API version while older versions already exist. Teams often assume that adding a new top-level model annotated with `@added` is sufficient, but validation still fails because referenced or parent models implicitly become visible to older versions. This behavior is not a tooling bug or PR‑specific issue; it reflects how TypeSpec versioning is designed.

In TypeSpec, version decorators such as `@added` apply only to the model where they are explicitly declared. Versioning does not propagate transitively through model references, inheritance chains, or generic parameters. As a result, when a new version introduces a model that references additional parent models or generic arguments, those newly introduced models must also be explicitly versioned. Otherwise, the validator detects that older API versions now “see” models that were never marked as added, triggering migration or suitability errors.

The **correct approach** is to treat versioning as a model-level concern rather than a graph-level concern. Any model that is newly introduced, whether it is a parent type, a generic argument, or a nested structure, must be annotated with @added in the same version. This makes version visibility explicit and avoids accidental exposure of new schemas to older API versions.

## TypeSpec Does Not Support Version-Based Conditional Imports

**Scenario**

In `main.tsp`, multiple resource type .tsp files are imported. When introducing a new API version, one of the resource types should not be included in that new version. Is it supported to restrict importing TypeSpec files in main.tsp based on API versions?

**Key Takeaway**

TypeSpec does not support conditional imports by API version. 

**Recommended Approach**

Use the TypeSpec versioning library and mark models, resource types, operations, or properties that should no longer appear in a specific API version with the `@removed` decorator.
Version-specific inclusion or exclusion is controlled via versioning decorators, not via import statements.