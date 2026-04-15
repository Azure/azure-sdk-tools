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

This is enforced by the `@azure-tools/typespec-azure-resource-manager/arm-no-record` linting rule. `{}` (empty object type) is also invalid in ARM TypeSpec specs.

**If you truly need an arbitrary key/value dictionary**, you must get architecture/review board approval first. Then explicitly suppress the rule with justification:

```typespec
#suppress "@azure-tools/typespec-azure-resource-manager/arm-no-record" "Approved free-form metadata bag"
metadata?: Record<string>;
```

This should be rare — arbitrary dictionaries are strongly discouraged in ARM.

**Important: If this is actually resource tags**, do NOT redefine them. Use the built-in ARM tags support already included in `TrackedResource`:

```typespec
model MyResource is TrackedResource<MyProperties> {
  ...ResourceNameParameter<MyResource>;
  // tags are already included — do not add Record<string> tags property
}
```

Or use `...Foundations.ArmTagsProperty` explicitly. ARM tags already use `Record<string>` internally and are exempt from the `arm-no-record` rule.

This guidance applies to ARM (management plane) only.

## ARM PUT LRO patterns: do NOT model 202 on PUT for new services

Per ARM RPC guidelines, the required pattern for ARM resource PUT is: return `201` for create and `200` for update. If the operation is long-running, use `x-ms-long-running-operation: true` and include the appropriate LRO headers (`Azure-AsyncOperation` and/or `Location`).

**Returning `202` for PUT is explicitly deprecated and not supported for new resource types.** This is an old model. For greenfield services (starting Jan 2025), `Azure-AsyncOperation` is required per the RPC contract.

If the service returns an `Azure-AsyncOperation` polling header on the `200` response, it is OK to model it — but only if the operation is actually long-running. The standard ARM TypeSpec templates already handle this correctly: `200` and `201` responses with the resource body, and LRO headers on the create response.

**Choosing between `ArmResourceCreateOrUpdateAsync` vs `ArmResourceCreateOrReplaceAsync`:**
- `ArmResourceCreateOrUpdateAsync`: 201 response contains `Azure-AsyncOperation` LRO header. Use when final-state polling should use `azure-async-operation`.
- `ArmResourceCreateOrReplaceAsync`: 201 response contains `Location` LRO header. Use when final-state polling should use `location`.

```typespec
@armResourceOperations
interface ManagedClusters {
  // Produces 200/201 + x-ms-long-running-operation and final-state-via azure-async-operation.
  createOrUpdate is ArmResourceCreateOrReplaceAsync<ManagedCluster>;
}
```

If you truly must keep 202-on-PUT behavior (brownfield), you should discuss with ARM/API review because it conflicts with the current ARM contract guidance for PUT. The guidance is to fix the service to return `200/201` for PUT (even when async) and rely on LRO headers + provisioningState.

Use standard ARM templates so the LRO metadata is consistent (headers + `x-ms-long-running-operation` + `final-state-via`). Do not invent one-off patterns unless absolutely necessary.

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

## Different path parameter names for parent resource per child operation are not directly supported

ARM resource operation keys are derived from parent keys and cannot be individually specified per operation. To use a different parent key name (e.g., `jobName` instead of `name`) for a specific child operation, you would need a custom operation definition with explicitly specified keys.

**Recommended**: Change the key name globally using the `@key` decorator on the parent resource's name property unless there is a blocking reason to keep different names.

## TypeSpec ProviderHub emitter is about generating RPaaS extensions, not about generating APIs

When using the providerhub template to generate a new RP, adding a custom API in main.tsp does not generate the new models or controllers after build.

The emitter is specifically about generating RPaaS extensions, not about generating general APIs.

The emitter only updates a specific set of folders. You can write your own controllers for APIs outside of the generated extensions, but make sure not to place them in the folder with generated artifacts.

## Excluding Large Properties from ARM List Responses

Standard `ArmResourceRead` and `ArmResourceListByParent` return the canonical resource model, with no built-in way to omit specific properties. To exclude large properties from list responses, override the response type using the `Response` parameter on the list operation templates.

```typespec
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

## Use Azure.ResourceManager.ExtensionResource, not @extensionResource decorator

For ARM extension resources, use `Azure.ResourceManager.ExtensionResource` as the base type rather than the `@extensionResource` decorator. Even if the resource has the same properties as a `TrackedResource`, using `ExtensionResource` is required because `resourceKind` matters when the service side calls `resolveArmResources`.

```typespec
model MyExtension is ExtensionResource<MyExtensionProperties> {
  @key("extensionName")
  @segment("extensions")
  @path
  name: string;
}
```

## Use @Rest.Private.actionSegment to preserve custom casing in action routes

The `@action` decorator normalizes the action segment to lowercase. To preserve custom casing (e.g., `All` instead of `all`), use `@Rest.Private.actionSegment` instead.

```typespec
@armResourceOperations
interface Employees {
  @Rest.Private.actionSegment("All")
  listAll is ArmResourceActionSync<Employee, Request, Response>;
}
```

Note: ARM APIs mandate case-insensitive path segments, so custom casing should not be needed for ARM. This workaround applies to non-ARM scenarios where `@action` normalization is problematic.

## ResourceNameParameter does not support union types for the Type parameter

Passing a union type to `ResourceNameParameter<..., Type = MyUnion>` throws "Cannot apply @pattern decorator to type it is not a string". This is a known bug.

**Workaround**: Manually define the name property with `@path`, `@segment`, `@key`, and `@visibility`, and suppress the `arm-resource-name-pattern` warning:

```typespec
model MyResource is TrackedResource<MyProperties> {
  #suppress "@azure-tools/typespec-azure-resource-manager/arm-resource-name-pattern"
  @visibility(Lifecycle.Read)
  @path
  @segment("resources")
  @key("resourceName")
  name: MyNameUnion;
}
```

## ARM PUT operations must not return 202 Accepted — it violates ARM RPC

ARM PUT operations that return `202 Accepted` violate the ARM RPC guidelines. No new ARM service should implement this pattern.

For brownfield conversion of existing APIs that already behave this way, it is possible to model the 202 response to prevent breaking changes. Use `ArmResourceCreateOrReplaceAsync` with custom LRO headers and suppress `arm-put-operation-response-codes`. This should only be done if you are certain the service actually returns 202 on PUT — SDK client flexibility may have masked errors in the original spec.

## Customize ARM Operations_List operationId by naming the interface

The default `interface Operations extends Azure.ResourceManager.Operations {}` produces `Operations_List` as the operationId. To change it (e.g., to `ProviderOperations_List`), rename the interface itself:

```typespec
interface ProviderOperations extends Azure.ResourceManager.Operations {}
```

Suppress the resulting naming warning. Do not use the `@operationId` decorator.

## How to implement a custom model for patch

You want to make sure to represent the properties that can be PATCHed in the patch operation, generally this includes any properties that are not generated on the service (e.g. type, id, name, systemData) or settable only on creation (e.g. location), and make sure these properties are optional and have no defaults.

**You can do this either be creating a separate model type for patch, or by using standard transformations to filter out the readOnly and createOnly properties and make these optional.**

- Here is a sample of using a fully bespoke patch model:

```
model MyResourceUpdate {
  ...Azure.ResourceManager.Foundations.ArmTagsProperty;
  properties?: MyResourcePropertiesUpdate;
}

model MyResourcePropertiesUpdate {
  fieldA?: string;
  fieldB?: int32;
}

@armResourceOperations
interface MyResources {
  update is ArmCustomPatchSync<MyResource, MyResourceUpdate>;
}
```

- Here is a sample of using transformations:

```
alias PatchModel<
  T extends {},
  OmittedProperties extends string = "",
  NameTemplate extends valueof string = "{name}Update"
> = UpdateableProperties<
      OptionalProperties<
        OmitDefaults<OmitProperties<T, OmittedProperties>>
      >
    >;

model MyResourcePropertiesUpdate is PatchModel<MyResourceProperties>;

model MyResourceUpdate
  is PatchModel<MyResource, OmittedProperties = "name" | "properties"> {
  properties?: MyResourcePropertiesUpdate;
}

@armResourceOperations
interface MyResources {
  update is ArmCustomPatchSync<MyResource, MyResourceUpdate>;
}
```

The advantage to using transformations is that, as the model versions, the PATCH model will also version automatically, whereas if you use a completely bespoke patch model, you will need to remember to make appropriate versioning changes there as well as in the resource model.