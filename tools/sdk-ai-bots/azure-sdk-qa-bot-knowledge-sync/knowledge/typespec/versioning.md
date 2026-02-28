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

