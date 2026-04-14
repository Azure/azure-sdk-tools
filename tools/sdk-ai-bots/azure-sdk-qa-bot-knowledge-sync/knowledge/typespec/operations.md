# TypeSpec Operations

Common issues and solutions for defining and structuring API operations in TypeSpec.

## TypeSpec does not have built-in JSON merge-patch support (as of 2025)

TypeSpec currently does **not** have built-in or explicit support for `application/merge-patch+json` (JSON Merge Patch). There are no dedicated keywords or types in the language to model it directly.

**Recommended**: Manually define a separate PATCH model for each resource. This model mirrors the resource structure with all properties made optional, expressing the merge-patch behavior where only specified fields are updated.

```typespec
model MyResourcePatch {
  name?: string;
  description?: string;
  tags?: Record<string>;
}

@patch
@route("/resources/{id}")
op update(
  @path id: string,
  @header("content-type") contentType: "application/merge-patch+json",
  @body body: MyResourcePatch,
): MyResource;
```

Do **not** use `| null` in the model to indicate erasable fields. Merge-patch support is treated as a fundamental protocol decision of the service, not something reflected in the type system. A new `MergePatch` template is being designed to address this gap for generation-first languages.

## PATCH requests over discriminated types should include the discriminator property

When modeling a PATCH operation for a resource that uses a discriminated union (polymorphic type with a `@discriminator` field), the PATCH request body should **require the discriminator property**. This is important because the service typically needs the discriminator value on the wire to determine how to apply the PATCH to the existing resource.

Using `ArmResourcePatchAsync` with a discriminated resource type may trigger OAV errors like `OBJECT_MISSING_REQUIRED_PROPERTY_DEFINITION` because the generated patch model may not include the discriminator as required.

**Recommended**: Include the discriminator property as required in the PATCH body. This may result in some LintDiff violations (since PATCH bodies are normally all-optional), but those violations are expected and can be suppressed with the `Approved-LintDiff` label. This is the suggested approach when the PATCH semantics require the discriminator value to be present.

