# TypeSpec Validation

Common issues and solutions for TypeSpec validation errors and CI fixes.

## TypeSpec installation fails with spawn EINVAL error on Windows

This error typically indicates an outdated globally installed TypeSpec compiler. Update to the latest version:

```bash
npm install -g @typespec/compiler@latest
```

If working in the `azure-rest-api-specs` repository, follow the repo-specific setup documentation rather than installing globally. Ensure you are using Node.js 20+ and npm 7+.

## Augmented decorators on resource name do not apply to LegacyOperations path parameters

Decorators like `@@minLength` and `@@maxLength` applied to a resource model's `name` property are not propagated to LegacyOperations path parameters. LegacyOperations uses passed-in parameters, not the resource model's `name`.

**Fix**: Define a named parameter model with the decorators applied and pass it to `LegacyOperations`:

```typespec
model MyResourceNameParameter {
  @maxLength(256)
  @minLength(1)
  @path
  resourceName: string;
}
interface MyResourceOperations {
  ...LegacyOperations<MyResource, MyResourceNameParameter>;
}
```

Note: Decorators can be applied to model statements, not model expressions.

## Namespace defaults when not explicitly set in tspconfig.yaml

When `namespace` is omitted from an emitter's config, it falls back to the `namespace` declared in the TypeSpec file. If none is declared in TypeSpec, the emitter derives a default from the resource provider name using language-specific conventions.

Management plane defaults per language: .NET → `Azure.ResourceManager.[ProviderName]`, Python → `azure-mgmt-[providername]`, Java → `com.azure.resourcemanager.[providername]`, JavaScript → `@azure/arm-[providername]`. Data plane follows the same pattern using the service group (e.g., `AI`) instead of `ResourceManager`.

To set a namespace globally for all emitters:

```yaml
namespace: Azure.LoadTesting
```

## Hiding an entire operation group from public SDK surface is not directly supported

TypeSpec does not support hiding a whole operation group via a decorator on the interface. The `@access` decorator is not interpreted by the client generator (tcgc) when applied to an interface.

**Fix**: Redefine the client in `client.tsp` to control which operations are exposed, then clean generated output and regenerate. As a per-language workaround in Python, rename the operation group in `_patch.py` (e.g., `client.evaluation_results` → `client._evaluation_results`).

When applying `@access(Access.internal)` to individual operations or a namespace, ensure those operations and their models are not also referenced by any public operation — any shared public reference forces them back to `public`.

## Sharing models between control plane and data plane

Create a shared namespace (e.g., `MyService.Shared`) that is not tied to either the control plane or data plane. Version the shared types independently and avoid importing `Azure.ResourceManager` in any shared code used by data plane.

Use `@useDependency` in each consuming namespace to declare which version of each dependency it targets:

```typespec
namespace MyService.DataPlane {
  enum Versions {
    @useDependency(Azure.Core.Versions.v1_0_Preview_2)
    @useDependency(MyService.Shared.Versions.v1)
    v2024_01_01: "2024-01-01",
  }
}
```

The error "Namespace '' is referencing types from versioned namespace 'Azure.Core' but didn't specify which versions" means a namespace is using versioned types without declaring `@useDependency`.

## visibility-sealed error when armResourceInternal is applied twice

`TrackedResource<T>` already applies `@Azure.ResourceManager.Private.armResourceInternal` internally. Adding it again explicitly on a model that uses `is TrackedResource<T>` causes "Visibility of property 'name' is sealed and cannot be changed."

**Fix**: Remove the redundant `@armResourceInternal` decorator. With `model is`, the decorator is already inherited from `TrackedResource<T>`. It was only needed when using `model extends` in older TypeSpec versions.

## readme.md is required for SDK generation (Avocado failure)

Even for brand-new TypeSpec-based APIs, Avocado validates that a `readme.md` exists somewhere in the spec directory to configure SDK generation. TypeSpec compilation succeeds without it, but the SDK pipeline needs it to determine which API versions to generate and where the OpenAPI files are.

The location and factoring of `readme.md` can vary by service. You need at least one in your service directory structure.

## PR bot adds WaitForARMFeedback label even when CI checks fail

This is intentional behavior — the label is added when the PR enters the ARM review queue regardless of CI status. Open a draft PR first and only mark it "ready for review" after all required checks pass, to avoid wasting reviewer time. Reviewers who see failing checks will manually change the label to `ARMChangesRequested`.

Note: Automatic label switching for failed checks is on the backlog and will be implemented once the labeling system migration to GitHub Actions is complete.

## Handling Documentation-Only TypeSpec Changes That Affect Multiple API Versions

When adding or updating model descriptions in TypeSpec, all API version specifications generated from TypeSpec change simultaneously because TypeSpec is the single source of truth. If only the latest version is updated while older versions are left unchanged, TypeSpec validation will fail.

All affected historical API specification versions should be updated together. Documentation-only updates should not be flagged as breaking changes.

## Stale local dependencies cause CI swagger mismatch, not config errors

When TypeSpec Validation fails with "Files have been changed after `tsp compile`", the most common cause is stale local dependencies producing different generated swagger than CI. The `SdkTspConfigValidation` config errors (for C#, Java, Go, Python emitters) are typically warnings only and not the actual failure cause.

**Fix**: Sync your branch and reinstall dependencies:

```bash
git fetch upstream main
git pull upstream main
npm ci
npx tsv specification/yourservice/YourService.Management
```

Always follow the TypeSpec Validation wiki instructions for local repro before investigating config issues.

## Inconsistent filename or folder casing causes TypeSpec validation failure on Linux CI

TypeSpec validation may pass on Windows (case-insensitive filesystem) but fail on Linux CI (case-sensitive). Common causes include inconsistent casing in the resource provider folder name (e.g., `Microsoft.PowerBIdedicated` vs `Microsoft.PowerBIDedicated`) or ARM namespace directory (e.g., `NGINX.NGINXPLUS` vs `Nginx.NginxPlus`), which creates two separate directories on Linux.

**Fix**: Ensure consistent casing in all file and folder names. Use `git status` on Linux or inspect CI output to detect duplicate directories. If you must keep non-standard casing, configure the TypeSpec output path explicitly in `tspconfig.yaml`.

## duplicate-type-name errors from unnamed transformation templates

Using transformation decorators like `@withoutDefaultProperties` directly or transformation templates like `Update<T>` without assigning a named model causes `duplicate-type-name` errors in TypeSpec Compiler v1.0.0+. This is a compiler bug, but the following anti-patterns should be avoided:

1. Do not use transformation decorators (e.g., `@withoutDefaultProperties`) directly
2. Do not use transformation templates without naming the result (e.g., `properties?: Update<MyProperties>`)

**Fix**: Create a named model for the transformed type:

```typespec
model MyPatchProperties
  is UpdateableProperties<OmitDefaults<MyBaseProperties>>;

model MyPatchModel {
  properties?: MyPatchProperties;
  ...ArmTagsProperty;
}
```