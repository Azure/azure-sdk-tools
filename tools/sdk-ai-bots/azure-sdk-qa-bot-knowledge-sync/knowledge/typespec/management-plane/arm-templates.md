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

`final-state-via: "original-uri"` means the original PUT request URI is polled to retrieve the final resource state. ARM defaults to `Azure-AsyncOperation` when that header is present, even for PUT operations.

Use `@useFinalStateVia` to override the default:

```typespec
@armResourceOperations
interface Employees {
  @useFinalStateVia("original-uri")
  create is ArmResourceCreateOrReplaceAsync<Employee>;
}
```

Valid values: `"original-uri"`, `"azure-async-operation"`, `"location"`. There is no special header type for `original-uri`; it refers to the original request URI directly.

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
