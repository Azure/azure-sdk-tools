# ARM Legacy Patterns

Legacy/deprecated TypeSpec patterns for backward compatibility with non-standard ARM resources.

## When to use legacy patterns instead of standard ones

Use legacy patterns for:
- Pre-existing Swagger specs that don't conform to standard ARM patterns
- Custom routing that doesn't follow ARM auto-route conventions
- Polymorphic/discriminated resources with non-standard discriminators
- Optional location on tracked resources
- Single-page lists without pagination
- External type references to types defined in other Swagger files

## How to define a discriminated (polymorphic) resource

```typespec
model MyBaseResource
  is Azure.ResourceManager.Legacy.DiscriminatedTrackedResource<
    MyBaseProperties, "kind"
  > {
  ...ResourceNameParameter<MyBaseResource>;
}
```

Also available: `DiscriminatedProxyResource` and `DiscriminatedExtensionResource` for proxy and extension resource variants.

## How to define a tracked resource with optional location

Standard tracked resources require `location`. Use legacy for optional:

```typespec
model MyResource
  is Azure.ResourceManager.Legacy.TrackedResourceWithOptionalLocation<MyProperties> {
  ...ResourceNameParameter<MyResource>;
}
```

## How to define single-page list operations (no pagination)

```typespec
@armResourceOperations
interface Employees {
  list is Azure.ResourceManager.Legacy.ArmListSinglePageByParent<Employee>;
  listBySub is Azure.ResourceManager.Legacy.ArmListSinglePageBySubscription<Employee>;
}
```

Returns results without `nextLink` pagination.

## How to use @armExternalType and @externalTypeRef for external Swagger types

Reference types defined in external Swagger files:

```typespec
@externalTypeRef("../external/types.json#/definitions/ExternalModel")
model ExternalModel {}
```

## How to use @armOperationRoute for custom routing

```typespec
@armOperationRoute("/custom/path/{param}")
op customOp(@path param: string): Response;
```

## How to rename a path parameter with @renamePathParameter

```typespec
@renamePathParameter("employeeName", "workerName")
model Employee is TrackedResource<EmployeeProperties> {
  ...ResourceNameParameter<Employee>;
}
```

## How to use @customAzureResource for non-standard ARM resources

Marks a model as an ARM resource that doesn't follow standard patterns (no id/name/type from common-types):

```typespec
@customAzureResource
model MyLegacyResource {
  id: string;
  name: string;
  customProperty: string;
}
```

## How to use legacy extension and private endpoint operations

```typespec
// Legacy extension operations
@armResourceOperations
interface MyExtensions {
  create is Azure.ResourceManager.Legacy.Extension.CreateOrReplaceAsync<MyExtension>;
  update is Azure.ResourceManager.Legacy.Extension.CustomPatchAsync<MyExtension, PatchModel>;
}

// Legacy private endpoint operations
@armResourceOperations
interface MyPEConnections {
  create is Azure.ResourceManager.Legacy.PrivateEndpoints.CreateOrReplaceAsync<MyPEC, Parent>;
  list is Azure.ResourceManager.Legacy.PrivateEndpoints.ListSinglePageByParent<MyPEC, Parent>;
}
```
