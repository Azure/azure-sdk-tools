# TypeSpec ARM Templates

Common issues and solutions for selecting and using ARM resource templates in TypeSpec.

## Dictionary pattern (Record<string>) is an ARM anti-pattern

**Scenario**: You want to use `Record<string>` or a dictionary pattern like `{ [key: string]: string }` in an ARM resource properties model to represent flexible metadata.

**Why This Is An Anti-Pattern**: The dictionary pattern defeats important ARM features that customers expect to be able to use:
- **Azure Resource Graph (ARG)**: Cannot query or index dynamic keys
- **Azure Policy**: Cannot enforce governance rules on unknown keys
- Cannot document supported keys in a discoverable way
- Cannot version the set of supported keys
- Clients cannot determine which keys are required vs. optional

**Recommendation**: Instead of using a dictionary, define explicit properties for each supported field, or use an array of key-value pairs if the set is truly dynamic.

**Example of the anti-pattern** (avoid this):
```typespec
model MyResourceProperties {
  @doc("The metadata")
  metadata?: Record<string>;  // ❌ Anti-pattern
}
```

**Better approach** (use explicit properties):
```typespec
model MyResourceProperties {
  @doc("The owner of the resource")
  owner?: string;
  
  @doc("The cost center")
  costCenter?: string;
  
  @doc("The environment (dev/staging/prod)")
  environment?: string;
}
```

**Alternative for truly dynamic keys** (use KVP array):
```typespec
model KeyValuePair {
  key: string;
  value: string;
}

model MyResourceProperties {
  @doc("Additional metadata as key-value pairs")
  metadata?: KeyValuePair[];
}
```

**Key Points**:
- Dictionary/Record patterns prevent Azure Resource Graph queries
- Azure Policy cannot enforce rules on dynamic keys
- Explicit properties provide better documentation and validation
- If you must support dynamic keys, use an array of key-value pairs
- This guidance applies to ARM (management plane) only; data plane has different constraints

## LRO final-state-via for PUT operations in ARM

**Scenario**: You have an ARM PUT operation with an LRO (Long Running Operation) and need to control how clients determine the final state location using `final-state-via`.

**Understanding `original-uri`**: For PUT operations, `final-state-via: "original-uri"` means the original URI in the PUT request is used to retrieve the resource after the operation completes. The resource includes a status field (typically `provisioningState`) that determines its state.

**Default Behavior**: For PUT operations, `original-uri` is generally the default. However, if there are multiple valid pathways to resolve the LRO (both an `Azure-AsyncOperation` header and a resource with status), the default for ARM PUT is to use `Azure-AsyncOperation` when it is present.

**How to Control `final-state-via`**: Use the `@useFinalStateVia` decorator to explicitly specify which method should be preferred:

```typespec
@armResourceOperations
interface Employees {
  get is ArmResourceRead<Employee>;
  
  @useFinalStateVia("original-uri")
  create is ArmResourceCreateOrReplaceAsync<Employee>;
}
```

**When Multiple Headers Are Present**: If your operation returns both `Azure-AsyncOperation` and `Location` headers, or if the resource itself has a status:
- Without `@useFinalStateVia`, ARM defaults to `Azure-AsyncOperation` for PUT
- Use `@useFinalStateVia("original-uri")` to prefer polling the original resource URI
- Use `@useFinalStateVia("azure-async-operation")` to explicitly prefer the async operation header
- Use `@useFinalStateVia("location")` to prefer the Location header

**Valid Values for PUT Operations**:
- `"original-uri"` - Poll the original resource URL (default for PUT without async headers)
- `"azure-async-operation"` - Poll the Azure-AsyncOperation URL
- `"location"` - Poll the Location header URL

**Key Points**:
- `original-uri` is specific to PUT operations (resource creation/update)
- You don't need a special header type for `original-uri`; it refers to the request URI
- Use `@useFinalStateVia` to explicitly control the behavior when multiple options exist
- ARM defaults to `Azure-AsyncOperation` when present, even for PUT

## LRO response modeling for async resource actions

**Scenario**: You want to define an async ARM resource action (POST operation) where the initial response has no body, but the long running operation returns a body when it reaches a `Succeeded` state.

**Current Required Pattern**: Model the action using `ArmResourceActionAsync` and include the final result type. The OpenAPI will currently include a `200` response that represents the eventual operation return value when the operation is resolved, along with the `202` response for the initial acceptance.

```typespec
/** A ContosoProviderHub resource */
model Employee is TrackedResource<EmployeeProperties> {
  ...ResourceNameParameter<Employee>;
}

/** Employee properties */
model EmployeeProperties {
  /** Age of employee */
  age?: int32;
  
  @visibility(Lifecycle.Read)
  provisioningState?: ProvisioningState;
}

/** Employee move request */
model MoveRequest {
  from: string;
  to: string;
}

/** Employee move response */
model MoveResponse {
  movingStatus: string;
}

@armResourceOperations
interface Employees {
  /** A sample resource action that move employee to different location */
  move is ArmResourceActionAsync<Employee, MoveRequest, MoveResponse>;
}
```

**What Gets Emitted**: This pattern produces:
- `202 Accepted` - Initial response (no body)
- `200 OK` - Final response with `MoveResponse` body
- LRO headers for polling (`Azure-AsyncOperation` or `Location`)

**Future Direction**: The goal is to move to a representation where the `200` response is not in the swagger and the return value is represented in the `x-ms-long-running-operation-options` extension (specifically `final-state-schema`). However, not all emitters for required languages support this yet, so the current pattern (with explicit `200` response) is still required.

**Key Points**:
- Use `ArmResourceActionAsync<Resource, Request, Response>` for async actions with response bodies
- The `Response` type represents the final result when the LRO succeeds
- The `200` response currently appears in Swagger for compatibility
- Future versions will rely more on `final-state-schema` for final result type information

## Adding final-state-schema for a single POST action LRO

**Scenario**: You want to add `final-state-schema` to a single POST action LRO operation without affecting all LRO operations in your spec (avoiding `emit-lro-options: "all"`).

**Solution**: The `final-state-schema` is emitted when that specific LRO's headers indicate a concrete `FinalResult` type. Set the `FinalResult` on the LRO headers for just that operation.

```typespec
model MyActionResponse {
  output: string;
}

@armResourceOperations
interface MyResources {
  @segment("myAction")
  myAction is ArmResourceActionAsync<
    MyResource,
    MyActionRequest,
    MyActionResponse,
    LroHeaders = ArmLroLocationHeader<FinalResult = MyActionResponse> &
      Azure.Core.Foundations.RetryAfterHeader
  >;
}
```

**For Azure-AsyncOperation polling**:
```typespec
@armResourceOperations
interface MyResources {
  @segment("myAction")
  myAction is ArmResourceActionAsync<
    MyResource,
    MyActionRequest,
    MyActionResponse,
    LroHeaders = ArmAsyncOperationHeader<FinalResult = MyActionResponse> &
      Azure.Core.Foundations.RetryAfterHeader
  >;
}
```

**Key Points**:
- Set `FinalResult = <YourResponseType>` on the LRO header type
- This scopes the `final-state-schema` emission to only that operation
- You don't need `emit-lro-options: "all"` globally
- For actions with no final response body, set `FinalResult = void`
- The `final-state-schema` in current emitters is primarily for debugging/documentation

## ARM path segments are case-insensitive

**Scenario**: Your existing Swagger has path segments with different casing (e.g., `/volumeGroups` in one place and `/volumegroups` in another), and TypeSpec generates a consistent casing that differs from parts of the original Swagger.

**ARM Guidance**: Static segments in ARM URLs are meant to be case-insensitive. The correct approach is to use consistent casing throughout, preferring the standard ARM type name format.

**Example Issue**:
```
/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ElasticSan/elasticSans/{elasticSanName}/volumeGroups
/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ElasticSan/elasticSans/{elasticSanName}/volumegroups/{volumeGroupName}
```

**Solution**: Choose one canonical casing (preferably the resource type name, which is typically PascalCase for the type name but lowercase in paths). In TypeSpec, the `@segment` decorator controls the path segment:

```typespec
model VolumeGroup
  is Azure.ResourceManager.ProxyResource<VolumeGroupProperties> {
  ...ResourceNameParameter<
    Resource = VolumeGroup,
    KeyName = "volumeGroupName",
    SegmentName = "volumegroups",  // Use consistent lowercase
    NamePattern = "^[A-Za-z0-9]+((-|_)[a-z0-9A-Z]+)*$"
  >;
}
```

**Favor Consistent Casing**:
- Use camelCase for static segments: `/resourceGroups` (not `/resourcegroups`)
- Be consistent across all operations for the same resource type
- Don't try to match exact casing from legacy Swagger if it's inconsistent
- ARM runtime treats these as case-insensitive, but tooling and documentation benefit from consistency

**Key Points**:
- ARM path segments are case-insensitive by design
- Choose one canonical casing and use it consistently
- Favor camelCase for static segments
- Don't preserve inconsistent casing from legacy Swagger
- TypeSpec templates like `ArmResourceListByParent` will use the segment from your resource model

## Annotating the same model with both SubscriptionLocationResource and ResourceGroupLocationResource is not supported

**Scenario**: You have a resource that needs to be accessible at both subscription scope (`/subscriptions/{subscriptionId}/providers/{RP}/locations/{location}/{resourceType}`) and resource group scope (`/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{RP}/locations/{location}/{resourceType}`) with the same resource type name.

**Current Limitation**: TypeSpec does not support annotating the same model with both `@subscriptionLocationResource` and `@resourceGroupLocationResource`. When you apply both, only one scope's paths are generated (typically the one applied first).

**Why This Happens**: In ARM's resource identity model, these are actually two different resource types with different scopes and potentially different operations. They must be registered as separate types in ARM.

**Workarounds**:

**Option 1: Create two separate resource types** (current requirement):
```typespec
// Subscription-scoped resource
@subscriptionLocationResource
model ValidatedSolutionRecipe
  is ProxyResource<ValidatedSolutionProperties> {
  ...
}

// Resource-group-scoped resource
@resourceGroupLocationResource
model ResourceGroupValidatedSolutionRecipe
  is ProxyResource<ValidatedSolutionProperties> {
  ...
}
```

**Option 2: Use shared properties model**:
If both resources have identical schemas, share the properties model to avoid duplication:
```typespec
model ValidatedSolutionProperties {
  // Shared properties
  version: string;
  publisher: string;
}

@subscriptionLocationResource
model ValidatedSolutionRecipe
  is ProxyResource<ValidatedSolutionProperties> {
  ...
}

@resourceGroupLocationResource  
model RGValidatedSolutionRecipe
  is ProxyResource<ValidatedSolutionProperties> {
  ...
}
```

**Option 3: Use custom operations**:
If operations for both resources are identical, consider using custom operations that reference the shared model instead of relying on the resource type decorators.

**Option 4: Consider RPaaS proxy resources**:
Using RPaaS (Resource Provider as a Service) extensions can help handle this scenario and simplify the API, reducing confusion for customers.

**Key Points**:
- ARM requires these to be registered as separate resource types due to different scopes
- You must create two models, even if they're semantically the same resource
- Share the properties model to minimize duplication
- Consider whether both scopes are truly necessary for your scenario
- RPaaS patterns may provide better alternatives
