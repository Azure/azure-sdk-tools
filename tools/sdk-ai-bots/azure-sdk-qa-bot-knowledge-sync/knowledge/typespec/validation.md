# TypeSpec Validation

Common issues and solutions for TypeSpec validation errors and CI fixes.

## TypeSpec installation fails with spawn EINVAL error on Windows

**Scenario**: When running `tsp install` or installing TypeSpec globally on Windows, you encounter an `Error: spawn EINVAL` error with `code: 'EINVAL'` and `syscall: 'spawn'`.

**Root Cause**: This error typically indicates an incompatibility issue with an older version of the TypeSpec compiler or a problem with the Node.js environment configuration on Windows.

**Solution**:

1. **Check the compiler version**: The error often occurs with outdated TypeSpec compiler versions. Verify which version you have installed globally:
   ```bash
   npm list -g @typespec/compiler
   ```

2. **Follow the official installation guide**: If you're working in the Azure REST API specs repository (`azure-rest-api-specs`), follow the documented setup process:
   - [TypeSpec REST API Development Process](https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/typespec-rest-api-dev-process.md)

3. **Update TypeSpec**: Install or update to the latest version:
   ```bash
   npm install -g @typespec/compiler@latest
   ```

4. **Verify Node.js and npm versions**: Ensure you're using supported versions (Node.js 20+ and npm 7+ are recommended).

5. **Check shell configuration**: If the issue persists, verify your shell configuration:
   ```bash
   npm config get script-shell
   ```

**Key Points**:
- This is primarily a Windows-specific Node.js process spawning issue
- Always use the latest TypeSpec compiler version
- Follow repository-specific setup documentation when working in Azure specs repos

## Augmented decorators on resource name do not apply to LegacyOperations path parameters

**Scenario**: When using augmented decorators like `@@minLength` and `@@maxLength` on a resource model's `name` property in a multi-path (nested resource) scenario with LegacyOperations, the constraints do not appear in the generated OpenAPI path parameters.

**Root Cause**: In LegacyOperations, the actual path parameters do not come from the resource model. The decorators applied to the resource's `name` property are not automatically propagated to the operation parameters because LegacyOperations uses passed-in parameters, not the resource model's name.

**Solution**: You need to decorate the parameters directly in the LegacyOperations instantiation. When you construct the LegacyOperations interface, define the parameters explicitly with the decorators applied.

```typespec
// Define parameters with decorators
model MyResourceNameParameter {
  @maxLength(256)
  @minLength(1)
  @path
  resourceName: string;
}

// Use in LegacyOperations
interface MyResourceOperations {
  // Pass the decorated parameter model
  ...LegacyOperations<MyResource, MyResourceNameParameter>;
}
```

**Important Constraint**: You can decorate a model statement, but not a model expression. If you need to apply decorators to name parameters, define them as named models first.

**Key Points**:
- Path parameters in LegacyOperations are passed-in parameters, not derived from resource model
- Decorators on resource model `name` are not propagated to LegacyOperations parameters
- Define and decorate parameters directly in the operations interface
- Consider creating a dedicated parameter model for reusability

## Namespace defaults when not specified in tspconfig.yaml

**Scenario**: When the `namespace` option is not explicitly specified in `tspconfig.yaml` for a language emitter (e.g., `@azure-tools/typespec-ts`), you need to understand what namespace will be used.

**Behavior**: When `namespace` is not specified in the emitter configuration, the behavior falls back to what's defined in the TypeSpec file itself (the `namespace` declaration). If no namespace is declared in TypeSpec, the emitter determines the default, which may vary by language and emitter.

**Management Plane (ARM) Conventions**:
The namespace is fairly standardized, derived from the resource provider name:
- **.NET**: `Azure.ResourceManager.[ProviderName]`
- **Python**: `azure-mgmt-[providername]`
- **Java**: `com.azure.resourcemanager.[providername]`
- **JavaScript**: `@azure/arm-[providername]`

The pattern strips prefixes like `Azure` or `Microsoft`, flattens separators, and applies language-specific naming conventions.

**Data Plane Conventions**:
Similar to management plane, but uses the service group (like `AI`, `Data`, etc.) instead of "ResourceManager". By default, it uses the namespace from the TypeSpec unless overridden.

**Overriding Namespace**:
To override the namespace across all language emitters, add it to `tspconfig.yaml`:

```yaml
namespace: Azure.LoadTesting
```

This flag overrides the namespace for all emitters that respect this setting.

**Key Points**:
- Namespace defaults to TypeSpec file's `namespace` declaration
- Each emitter may have different default behavior
- Management plane and data plane follow different but consistent patterns
- Explicit namespace in `tspconfig.yaml` overrides all emitter defaults

## How to hide an operation interface from public SDK surface

**Scenario**: You want to generate an operations interface but keep it hidden from the public Python/SDK surface (make it internal).

**Current Status**: TypeSpec currently does not support hiding an entire operation group directly via a single decorator on the interface.

**Recommended Approaches**:

**Approach 1: Use client.tsp to redefine your client structure**

Create a custom client definition in `client.tsp` that controls which operations appear on the public client. When you redefine the client structure this way, default service clients (and their `Operations` classes) should not be emitted.

Steps:
1. Define your client structure in `client.tsp`
2. Remove all previously generated SDK output before regenerating
3. Verify your custom client structure is complete
4. Regenerate the SDK

**Approach 2: Use `@access(Access.internal)` on operations or namespace**

```typespec
import "@azure-tools/typespec-client-generator-core";
using Azure.ClientGenerator.Core;

@route("/evaluationResults")
@operationGroup
@access(Access.internal)
namespace EvaluationResults {
  @route("/list")
  @get
  op list(): string[];
}
```

Or mark each operation individually:

```typespec
@route("/evaluationResults")
@operationGroup
namespace EvaluationResults {
  @route("/list")
  @get
  @access(Access.internal)
  op list(): string[];
}
```

**Approach 3: Use `_patch.py` for customization**

As a workaround, customize visibility by renaming in `_patch.py`, e.g., `client.evaluation_results` to `client._evaluation_results`.

**Important Constraints**:
- The `@access` decorator doesn't work on interfaces in the way you might expect
- If any types/operations in the interface are referenced by a public operation, they will be forced to `public` by access calculation rules
- Python doesn't always "hide" things the same way as C#/Java; internal items may still be generated but not importable from the public surface

**Key Points**:
- Best practice: Redefine the client properly in `client.tsp`
- Clean all generated code before regenerating
- `@access` decorator is not directly supported on interfaces for hiding
- Ensure internal operations are not referenced by public operations

## Sharing models between control plane and data plane

**Scenario**: You need to share TypeSpec models between a control plane (ARM) specification and a data plane specification, and encounter `@useDependency` errors when trying to reference versioned namespaces.

**Recommended Approach**:

✅ **Create a shared namespace** that's not tied to either control plane or data plane:
```typespec
// shared.tsp
namespace MyService.Shared {
  model CommonResourceId {
    resourceId: string;
  }
}
```

✅ **Version the shared types independently**, not tied to any specific API version:
- Keep shared models simple and stable
- Avoid frequent changes to shared contracts

✅ **Avoid ARM library dependencies in data plane** models:
- Don't import `Azure.ResourceManager` in data plane shared code
- Use aliases or common types for things like resource IDs
- Model concepts at the appropriate abstraction level

✅ **Use `@useDependency` to reference versioned namespaces**:
```typespec
@versioned(Versions)
namespace MyService.DataPlane {
  @useDependency(Azure.Core.Versions.v1_0_Preview_2)
  @useDependency(MyService.Shared.Versions.v1)
  enum Versions {
    v2024_01_01: "2024-01-01"
  }
}
```

**Common Errors and Solutions**:

Error: "Namespace '' is referencing types from versioned namespace 'Azure.Core' but didn't specify which versions with @useDependency"

Solution: This occurs when you compile a namespace that uses versioned library types without declaring dependency versions. Add appropriate `@useDependency` declarations to your service version enum.

**Key Points**:
- Shared namespaces should be version-independent or have their own versioning
- Avoid coupling shared types to either ARM or data plane specific libraries
- Always declare `@useDependency` when referencing versioned namespaces
- Keep shared models simple to minimize version conflicts

## Visibility-sealed error on resource name property

**Scenario**: When compiling TypeSpec that uses `TrackedResource<T>` with `is`, you encounter an error: "Visibility of property 'name' is sealed and cannot be changed."

**Root Cause**: The `TrackedResource` template already includes the `@Azure.ResourceManager.Private.armResourceInternal` decorator. When you use `model is TrackedResource<T>` and also apply `@armResourceInternal` to your model, you're applying the decorator twice, which causes the visibility sealing conflict.

**Solution**: Remove the redundant `@armResourceInternal` decorator from your model. It's already applied by the `TrackedResource` template.

Before (incorrect):
```typespec
@Azure.ResourceManager.Private.armResourceInternal(FileSystemResourceProperties)
model FileSystemResource
  is Azure.ResourceManager.TrackedResource<FileSystemResourceProperties> {
  @path
  @key("fileSystemName")
  @segment("fileSystems")
  @visibility(Lifecycle.Read)
  name: string;
}
```

After (correct):
```typespec
model FileSystemResource
  is Azure.ResourceManager.TrackedResource<FileSystemResourceProperties> {
  @path
  @key("fileSystemName")
  @segment("fileSystems")
  @visibility(Lifecycle.Read)
  name: string;
}
```

**Why This Happens**: When you use `model extends` in earlier TypeSpec versions, the decorator from the base type wouldn't be inherited. But with `model is`, the decorator is included, making explicit re-application redundant and causing conflicts.

**Key Points**:
- `TrackedResource<T>` already includes `armResourceInternal`
- Do not apply `@armResourceInternal` when using `is TrackedResource<T>`
- If migrating from `extends` to `is`, remove the explicit decorator
- The suppression `@azure-tools/typespec-azure-core/composition-over-inheritance` is a hint this might be the issue

## README.md is required for SDK generation

**Scenario**: Your PR fails the Avocado validation step, complaining about missing `readme.md` files, even though you're submitting a brand new TypeSpec-based API.

**Root Cause**: A `readme.md` file is required somewhere in your spec directory structure to configure SDK generation. The location and factoring of `readme.md` files can vary by spec.

**Solution**: You need to create at least one `readme.md` file in your service directory to define SDK generation configuration. This file is used by the SDK generation pipeline to determine which SDKs to generate, which API versions to include, and other generation settings.

**Typical Location**:
```
specification/
  myservice/
    data-plane/
      myservice/
        readme.md          <-- SDK generation configuration
        tspconfig.yaml
        main.tsp
        stable/
          2024-01-01/
            myservice.json
```

**What to Include in readme.md**: The file should contain SDK generation configuration, such as:
- Which API versions to generate
- SDK language targets
- Package names and namespaces
- Input files (OpenAPI/Swagger files)

**Key Points**:
- `readme.md` is required for SDK generation, not for TypeSpec compilation
- You need at least one `readme.md` somewhere in your spec structure
- Location and factoring can vary by service
- Other services (like KeyVault) may have their `readme.md` files in different locations
- Avocado validates the presence and correct structure of SDK generation configuration

## PR bot adds WaitForARMFeedback label even when checks fail

**Scenario**: The PR bot adds the `WaitForARMFeedback` label to your PR even when required CI checks are failing.

**Current Behavior**: This is intended behavior and has always worked this way according to the ARM review team. The label indicates the PR is in the ARM review queue, regardless of CI status.

**Recommended Workflow**:
1. **Open a draft PR first**: Mark your PR as "Draft" when you first create it
2. **Fix all required checks**: Ensure all CI checks pass before requesting review
3. **Mark "ready for review"**: Only mark the PR as ready when all checks are green

**What Happens After Label is Added**:
- Reviewers typically see failing checks and manually comment "Fix X pipeline check"
- They then change the label to `ARMChangesRequested`
- You fix the issues and re-run pipelines

**Future Improvement**: The team is working on automatically changing the label to `ARMChangesRequested` when checks fail. This improvement is on the backlog and will be easier once the labeling system migration to GitHub Actions is complete.

**Best Practice**: Don't push commits expecting checks to fail and wait for manual label removal. Instead, use draft PRs and only mark ready for review when CI is green.

**Key Points**:
- `WaitForARMFeedback` label is added when PR enters review queue, regardless of CI
- This is intended behavior, not a bug
- Use draft PRs to develop with failing checks
- Only mark "ready for review" after all checks pass
- Automatic label switching for failed checks is on the backlog
