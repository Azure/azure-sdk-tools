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

## Modeling mutually exclusive properties with discriminated unions

TypeSpec has no built-in way to enforce mutual exclusivity between properties. Making both optional to "allow only one" is actually a breaking change if either was previously required.

**Recommended**: Use a discriminated union with `@discriminator` to model mutually exclusive variants. This is the approved Azure pattern.

```typespec
@discriminator("kind")
union CloudProfile {
  aws: AwsCloudProfile,
  gcp: GcpCloudProfile,
}

model AwsCloudProfile {
  kind: "aws";
  accountId: string;
}

model GcpCloudProfile {
  kind: "gcp";
  projectId: string;
}
```

The `@discriminated` decorator exists for more flexible patterns but is not yet supported by all SDK emitters. Use `@discriminator` for now.

## Modeling Mutually Exclusive Properties in TypeSpec for Azure APIs

**Scenario**

A model needs to enforce that **only one of multiple property objects can be specified**. An attempt is made to use **discriminated unions** to model this exclusivity.

**Guidance**

For Azure APIs, **discriminated unions are not currently supported by Azure emitters**, so they are not a viable solution. Additionally, modeling a **polymorphic resource type** (a single resource with multiple mutually exclusive RP‑specific schemas) is considered an **anti‑pattern**.

The recommended approaches are:

- Define **separate resources** instead of one polymorphic resource, or
- Make a **property within the RP‑specific properties polymorphic**, using **inheritance‑based discrimination** rather than discriminated unions.

While it is technically possible to use a discriminator property (such as a `kind` field) at the resource level, this is discouraged and should be avoided.
