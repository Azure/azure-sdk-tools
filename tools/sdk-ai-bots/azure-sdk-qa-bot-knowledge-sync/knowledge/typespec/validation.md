# TypeSpec Validation

Common issues and solutions for TypeSpec validation errors and CI fixes.

## TypeSpec installation fails with spawn EINVAL error on Windows

This error typically indicates an outdated globally installed TypeSpec compiler. Update to the latest version:

```bash
npm install -g @typespec/compiler@latest
```

If working in the `azure-rest-api-specs` repository, follow the repo-specific setup documentation rather than installing globally. Ensure you are using Node.js 20+ and npm 7+.

## Hiding an entire operation group from public SDK surface is not directly supported

TypeSpec does not support hiding a whole operation group via a decorator on the interface. The `@access` decorator is not interpreted by the client generator (tcgc) when applied to an interface.

**Fix**: Redefine the client in `client.tsp` to control which operations are exposed, then clean generated output and regenerate. As a per-language workaround in Python, rename the operation group in `_patch.py` (e.g., `client.evaluation_results` → `client._evaluation_results`).

When applying `@access(Access.internal)` to individual operations or a namespace, ensure those operations and their models are not also referenced by any public operation — any shared public reference forces them back to `public`.

## Sharing models between control plane and data plane

To prevent ARM library dependencies in the data plane, use aliases or common types for modeling items like resource IDs.

Apply `@useDependency` when referencing versioned namespaces such as `Azure.Core` or `Azure.ResourceManager`.

## visibility-sealed error when armResourceInternal is applied twice

`TrackedResource<T>` already applies `@Azure.ResourceManager.Private.armResourceInternal` internally. Adding it again explicitly on a model that uses `is TrackedResource<T>` causes "Visibility of property 'name' is sealed and cannot be changed."

**Fix**: Remove the redundant `@armResourceInternal` decorator. With `model is`, the decorator is already inherited from `TrackedResource<T>`. It was only needed when using `model extends` in older TypeSpec versions.

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

## GitHub Actions Example Validation Fails Due to Spec Constraints

When using tools like `oav generate-examples` to auto‑generate Swagger examples for a TypeSpec‑based spec, the generated examples may **not satisfy constraints defined in the spec**, such as string patterns or required formats. This is expected behavior: the example generator primarily produces a **structural placeholder**, not fully valid values.

Common validation failures indicate that:

- Example strings do not match required patterns
- Example values do not satisfy required formats

In most cases, the validation error message explicitly instructs you to replace the placeholder with a compliant value. The intended workflow is to **manually update the auto‑generated example values** so they conform to the constraints. Full placeholder materialization is not automated today and has not been prioritized, so manual adjustment is required to pass CI validation.

## Generating and Maintaining Examples in TypeSpec and Swagger

When generating examples for a TypeSpec‑based API, tools like `oav generate-examples` are intended as **best‑effort helpers** and do not fully control naming, synchronization, or semantic correctness on their own.

**Example naming** is determined by the `title` field inside the example JSON, not by the file name. TypeSpec and `tsv` do not enforce naming conventions for example files as long as they are placed in the configured examples directory. However, an example may be dropped if another example shares the same `title` and `operationId`.

`oav generate-examples` is designed to help bootstrap examples from Swagger, but the generated output often requires **manual editing**. It may rename examples, remove duplicates, or drop entries when later processed by `tsv`.

The **source of truth for examples is the `examples` folder alongside the TypeSpec sources**. During compilation, `tsp compile` / `tsv` copies these examples into the generated Swagger output. Swagger examples should not be treated as authoritative or manually synchronized.

For minimum‑set examples, generated responses may still include properties that are **required by the resource model**. For tracked resources, required properties (such as those mandated by the resource shape) will appear even when generating minimal example sets.

## Creating Examples for TypeSpec Models in Azure API Specs

In Azure API specifications, **examples are required at the operation level**, not at the model level. The `@example()` decorator on models is not supported for Azure API Specs and should not be used to satisfy example requirements.

The expected workflow is to **provide operation examples**, which naturally include serialized representations of the models used by those operations. Models themselves do not have `operationId`s; instead, examples are associated with operations via the `operationId` defined in the OpenAPI document.

The recommended process is:

- Design the API in TypeSpec.
- Compile the TypeSpec to generate OpenAPI.
- Use `oav` to generate example files for each operation, which include populated `operationId` and `title` fields.
- Manually review and refine the generated examples to improve documentation quality or cover important scenarios.
- Place the finalized example files under the `examples/<api-version>/` folder in the TypeSpec source tree, where they are automatically associated with the corresponding operations based on `operationId` and `title`.

## Cannot Viewing CI Workflow Details for Azure REST API PRs

When a contributor cannot view validation workflow details on an Azure REST API pull request and sees a message such as **“at least one review required to see the workflow”**, the issue is due to **insufficient permissions**.

Access to detailed workflow logs requires the appropriate Azure SDK or repository permissions. Contributors must request or obtain access through the official Azure SDK onboarding and permissions process.

**Resolution**

To gain visibility into workflow runs and validation errors, request the required permissions as described at:  
https://aka.ms/azsdk/access