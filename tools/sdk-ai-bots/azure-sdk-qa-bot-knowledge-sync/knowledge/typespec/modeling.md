
# TypeSpec Modeling

Common issues and solutions for API Modeling with TypeSpec.

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