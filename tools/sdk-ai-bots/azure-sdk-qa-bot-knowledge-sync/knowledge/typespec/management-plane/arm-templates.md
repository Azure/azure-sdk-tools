# TypeSpec ARM Templates

Common issues and solutions for selecting and using ARM resource templates in TypeSpec.

## Dictionary pattern (Record<string>) is an ARM anti-pattern

`Record<string>` and index-signature dictionaries defeat ARM features customers expect: Azure Resource Graph cannot query dynamic keys, and Azure Policy cannot enforce governance rules on unknown keys.

**Recommended**: Define explicit properties for each supported field. If keys are truly dynamic, use an array of key-value pairs instead.

```typespec
// ❌ Anti-pattern
model MyResourceProperties {
  metadata?: Record<string>;
}
// ✅ Preferred: explicit properties or KVP array
model KeyValuePair { key: string; value: string; }
model MyResourceProperties {
  metadata?: KeyValuePair[];
}
```

This guidance applies to ARM (management plane) only.

## Use @useFinalStateVia to control LRO final-state resolution in ARM PUT operations

Final-state can be retrieved from `"original-uri"`, `"azure-async-operation"`, `"location"`. ARM defaults to `Azure-AsyncOperation` when that header is present, even for PUT operations.

Use `@useFinalStateVia` to override the default. For example, to override to `original-uri`:

```typespec
@armResourceOperations
interface Employees {
  @useFinalStateVia("original-uri")
  create is ArmResourceCreateOrReplaceAsync<Employee>;
}
```

## Async ARM resource action with no immediate body but final result on success

Use `ArmResourceActionAsync<Resource, Request, Response>` where `Response` is the type returned in the `Succeeded` state. This currently emits both a `202 Accepted` (no body) and a `200 OK` with the response body, which is required by current lintdiff rules.

```typespec
@armResourceOperations
interface Employees {
  move is ArmResourceActionAsync<Employee, MoveRequest, MoveResponse>;
}
```

The future direction is to represent the final result only in `x-ms-long-running-operation-options` via `final-state-schema`, but not all language emitters support this yet.

## Adding final-state-schema for a single POST action LRO

Set `FinalResult` on the LRO header type for that specific operation to emit `final-state-schema` scoped to only that operation, without requiring `emit-lro-options: "all"` globally.

```typespec
myAction is ArmResourceActionAsync<
  MyResource,
  MyActionRequest,
  MyActionResponse,
  LroHeaders = ArmAsyncOperationHeader<FinalResult = MyActionResponse> &
    Azure.Core.Foundations.RetryAfterHeader
>;
```

For `Location`-based polling use `ArmLroLocationHeader<FinalResult = MyActionResponse>` instead. For actions with no final response body, set `FinalResult = void`.

## ARM path segments are case-insensitive; use consistent casing

Static segments in ARM URLs are treated as case-insensitive at runtime. If existing Swagger has inconsistent casing (e.g., `/volumeGroups` and `/volumegroups`), the Swagger is incorrect. Use a single canonical casing throughout and do not try to preserve legacy inconsistencies.

```typespec
...ResourceNameParameter<
  Resource = VolumeGroup,
  SegmentName = "volumegroups",  // consistent lowercase
>;
```

Favor camelCase for fixed segments (e.g., `/resourceGroups`). TypeSpec templates such as `ArmResourceListByParent` automatically use the segment from the resource model.

## Same model cannot have both SubscriptionLocationResource and ResourceGroupLocationResource scopes

TypeSpec does not support annotating one model with both `@subscriptionLocationResource` and `@resourceGroupLocationResource`. These are two distinct ARM resource types with different scopes and must be registered separately.

**Fix**: Create two separate resource models that share the same properties model:

```typespec
model SharedProperties {
  version: string;
  publisher: string;
}

@subscriptionLocationResource
model ValidatedSolutionRecipe
  is ProxyResource<SharedProperties> { ... }

@resourceGroupLocationResource
model RGValidatedSolutionRecipe
  is ProxyResource<SharedProperties> { ... }
```

Alternatively, consider RPaaS proxy resources which can simplify cross-scope scenarios.

## Modeling ARM resources with legacy ResourceModelWithAllowedPropertySet in TypeSpec: why it is invalid and how to represent it correctly

**When migrating Swagger ARM specifications to TypeSpec, a question is whether a resource that previously used ResourceModelWithAllowedPropertySet can directly extend it in TypeSpec.** This usually arises from Swagger patterns where a resource schema uses allOf to combine common ARM resource fields with a flattened properties object. In TypeSpec, attempting to define a resource model that extends ResourceModelWithAllowedPropertySet and then apply @armResourceInternal results in an error indicating an invalid base type, because this decorator only supports real ARM resource base types such as TrackedResource, ProxyResource, or ExtensionResource.
**The core reason** is semantic, not tooling limitation. ResourceModelWithAllowedPropertySet exists as a legacy compatibility helper used in limited examples; it is not treated as an actual ARM resource type in TypeSpec. As a result, it cannot represent a true resource boundary and is intentionally rejected by ARM resource validation. Carrying this pattern forward would preserve Swagger-era modeling quirks and make the spec harder to evolve.
**The correct approach** is to model the resource as a proper ARM resource type, typically TrackedResource, and reuse the legacy “allowed property set” via composition rather than inheritance. Resource-specific fields should live in a separate properties model and be spread or referenced, keeping the resource definition accurate and future-proof. This approach aligns with TypeSpec ARM guidelines, avoids invalid base types, and produces clearer, more maintainable specifications and SDKs.

## Customizing Parent Resource Key Names for Child Operations

1.  **How key names are determined**  
    Child operations inherit the parent resource key name from the resource path template.  
    The key is **not defined per operation**.

2.  **Why per‑operation renaming doesn’t work**  
    `@clientName` on an operation cannot rename a parent resource key.  
    SDKs derive parameter names from the **resource key**, not from individual operations.

3.  **Recommended solution**  
    If SDK reviews require a different key name (e.g. `jobName`), **rename the parent resource key globally** using the `@key` decorator on the resource property.  
    This keeps all child operations consistent and avoids special cases.

**Conclusion**  
Using different key names for different child operations is not supported or recommended.  
Rename the parent resource key once; all derived operations will follow.

## the typespec-providerhub emitter is about generating RPaaS extensions, not about generating APIs

When using the providerhub template to generate a new RP, adding a custom API in main.tsp does not generate the new models or controllers after build.

The emitter is specifically about generating RPaaS extensions, not about generating general APIs.

The emitter only updates a specific set of folders. You can write your own controllers for APIs outside of the generated extensions, but make sure not to place them in the folder with generated artifacts.

## Excluding Large Properties from ARM List Responses

**Scenario**

An ARM resource contains a required property that holds very large text and is not suitable for LIST responses.

While ArmResourceCreateOrReplaceAsync and ArmResourcePatchAsync allow overriding the request/response shape, standard ArmResourceRead and ArmResourceListByParent operations return the canonical resource model and do not provide a built‑in way to omit specific properties.

**Supported Approach**

For list operations, you can **override the response type by using the Response parameter on the list operation templates**, instead of changing the resource model itself.

**Example**

```
@armResourceOperations
interface Employees {
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-operation-response"
  listByResourceGroup is ArmResourceListByParent<
    Employee,
    Response = EmployeeListResult
  >;
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-operation-response"
  listBySubscription is ArmListBySubscription<
    Employee,
    Response = EmployeeListResult
  >;
}
```