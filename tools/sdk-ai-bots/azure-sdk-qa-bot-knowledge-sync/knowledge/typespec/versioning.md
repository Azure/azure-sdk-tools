# TypeSpec Versioning

Common issues and solutions for API versioning and avoiding breaking changes in TypeSpec.

## How to use @added decorator for service versioning

**Scenario**: You need to add a new API version to your TypeSpec service and want to understand what the `@added` decorator does and how it affects SDKs.

**What `@added` Does**: The `@added(<version>)` decorator (from `@typespec/versioning`) declares that a model, operation, property, or parameter is introduced starting at a particular API version. It's part of TypeSpec's versioning model that allows tooling (including emitters and SDK generators) to understand which API surface exists at which version.

**Usage Pattern**:
```typespec
import "@typespec/versioning";
using Versioning;

@service
@versioned(Versions)
namespace My.Service;

enum Versions {
  v2024_01_01: "2024-01-01",
  v2024_02_01: "2024-02-01",
}

model UserInfo {
  username: string;
}

@route("/users/info")
op getUserInfo(@body userInfo: UserInfo): void;

// This operation is added in v2024_02_01
@added(Versions.v2024_02_01)
@route("/users/profile")
op createUserProfile(@body profile: { profileName: string }): void;
```

**Impact on Generated Artifacts**:
- **OpenAPI/Swagger**: When emitting for v2024_01_01, `createUserProfile` won't appear. When emitting for v2024_02_01, it will appear.
- **SDKs**: Azure SDK generation guidance explicitly uses `@added` to indicate when a feature was first introduced. This helps avoid confusing experiences for customers using older-version clients—operations that didn't exist in older versions shouldn't appear or be callable for those versions.

**Other Versioning Decorators**:
- `@removed(<version>)` - Marks when a type was removed (always a breaking change)
- `@renamedFrom(<version>, <oldName>)` - Documents a rename from the old name
- `@typeChangedFrom(<version>, <oldType>)` - Documents a type change
- `@madeOptional(<version>)` - Documents when a required property became optional

**Key Points**:
- `@added` is not just for Swagger; it affects SDK generation and versioning semantics
- Tag each new model, property, operation, or parameter with the version it was introduced
- TypeSpec uses these decorators to reconstruct the API at each version
- Versioning decorators themselves cannot be versioned (limitation)
- Types referenced by versioned elements generally need to be versioned too

## Using @useDependency for client.tsp versioning

**Scenario**: Your service has a `client.tsp` file with customizations, and you need to understand what `@useDependency` means and how to handle customizations that work across multiple versions.

**What `@useDependency` Does**: When a namespace references types from a versioned library (like `Azure.Core` or your main service namespace), `@useDependency` declares which version of that library the referencing namespace is targeting. This is required for version resolution.

**When It's Required**: If your `client.tsp` customization namespace is **not** a child namespace of your versioned service namespace, you must explicitly declare version dependencies.

**Pattern for Cross-Version Customizations**:

Create a versions enum in your client namespace that mirrors the service versions, and use `@useDependency` to bind them:

```typespec
// main.tsp - Service definition
@versioned(Contoso.WidgetManager.Versions)
namespace Contoso.WidgetManager {
  enum Versions {
    @useDependency(Azure.Core.Versions.v1_0_Preview_2)
    v2022_08_30: "2022-08-30",
    
    @useDependency(Azure.Core.Versions.v1_0_Preview_2)
    v2025_01_30: "2025-01-30",
  }
  
  // Service models and operations...
}

// client.tsp - Client customizations
@versioned(ClientVersions)
namespace ContosoClient {
  using Contoso.WidgetManager;
  
  enum ClientVersions {
    @useDependency(Azure.Core.Versions.v1_0_Preview_2)
    @useDependency(Contoso.WidgetManager.Versions.v2022_08_30)
    v2022_08_30: "2022-08-30",
    
    @useDependency(Azure.Core.Versions.v1_0_Preview_2)
    @useDependency(Contoso.WidgetManager.Versions.v2025_01_30)
    v2025_01_30: "2025-01-30",
  }
  
  // Client customizations that apply to both versions...
}
```

**If Customizations Are Version-Specific**: Use `@added`, `@removed`, etc. within the client namespace just like in the service namespace.

**If Client.tsp is a Child Namespace**: If your `client.tsp` defines a namespace that's a child of the versioned service namespace, no explicit `@useDependency` is needed—it inherits the version context.

**Key Points**:
- `@useDependency` binds a namespace to specific versions of its dependencies
- Required when referencing versioned namespaces from outside their namespace tree
- Create a mirrored version enum in client.tsp that maps to service versions
- This pattern allows customizations to apply across multiple versions
- Version-specific customizations use the same `@added`/`@removed` patterns as service code

## Changing a property from required to optional using @madeOptional

**Scenario**: You're releasing a new API version and want to change a property from required to optional.

**Wrong Approach** (doesn't work):
```typespec
@doc("Translation input.")
model TranslationInput {
  @typeChangedFrom(ApiVersions.v2024_05_20_preview, localeName)
  sourceLocale?: localeName;
}
```

**Why It Doesn't Work**: `@typeChangedFrom` is for when you're changing the *type* of a property. Changing from required to optional is a *different kind of change*.

**Correct Approach**: Use the `@madeOptional` decorator from `@typespec/versioning`:

```typespec
import "@typespec/versioning";
using TypeSpec.Versioning;

scalar localeName extends string;

@doc("Translation input.")
model TranslationInput {
  @madeOptional(ApiVersions.v2026_02_01)
  sourceLocale?: localeName;
}
```

**How It Works**:
- The property is defined as optional (`?`) in the TypeSpec model (representing the current/latest version)
- `@madeOptional(ApiVersions.v2026_02_01)` tells TypeSpec that in versions before `v2026_02_01`, this property was required
- When emitting the older version, TypeSpec will generate the property as required
- When emitting the new version, the property is optional

**Important Note**: This change is considered a **breaking change** in API design. While TypeSpec supports modeling it, you should consider the impact on existing clients.

**Key Points**:
- Use `@madeOptional` when changing required to optional
- Put the version where it becomes optional as the argument
- The property should be marked optional (`?`) in the model
- TypeSpec interprets the model as the "current" state; decorators describe past states
- This is considered a breaking change and should be handled with care

