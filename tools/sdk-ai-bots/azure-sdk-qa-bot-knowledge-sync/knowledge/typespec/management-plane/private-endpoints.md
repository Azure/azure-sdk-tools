# ARM Private Endpoints

How to add private endpoint and private link support to an ARM resource in TypeSpec.

## How to add private endpoint connection CRUD operations

Define a private endpoint connection resource and create an operations interface using the built-in templates.

```typespec
model MyPrivateEndpointConnection is PrivateEndpointConnectionResource {}

alias PrivateEndpointOps = Azure.ResourceManager.Private.PrivateEndpoints<
  MyPrivateEndpointConnection
>;

@armResourceOperations
interface PrivateEndpointConnections {
  list is PrivateEndpointOps.ListByParent<ParentResource>;
  get is PrivateEndpointOps.Read<ParentResource>;
  createOrUpdate is PrivateEndpointOps.CreateOrUpdateAsync<ParentResource>;
  delete is PrivateEndpointOps.DeleteAsyncBase<
    ParentResource,
    ArmAcceptedLroResponse | ArmDeletedResponse | ArmDeletedNoContentResponse
  >;
}
```

Swagger generates:
- `GET .../parentResources/{name}/privateEndpointConnections` — list
- `GET .../privateEndpointConnections/{connectionName}` — get
- `PUT .../privateEndpointConnections/{connectionName}` — create (LRO)
- `DELETE .../privateEndpointConnections/{connectionName}` — delete (LRO)

All definitions reference `common-types/privatelinks.json#/definitions/PrivateEndpointConnection`.

## How to add private link resource operations

```typespec
@armResourceOperations
interface PrivateLinkResources {
  list is Azure.ResourceManager.PrivateLinks<MyPrivateLinkResource>
    .ListByParent<ParentResource>;
  get is Azure.ResourceManager.PrivateLinks<MyPrivateLinkResource>
    .Read<ParentResource>;
}
```

Swagger: GET at `.../privateLinkResources` (list) and `.../privateLinkResources/{name}` (get).

## How to use the composite PrivateEndpoints interface shortcut

```typespec
interface MyPEConnections
  extends Azure.ResourceManager.PrivateEndpoints<MyPrivateEndpointConnection> {}
interface MyPLResources
  extends Azure.ResourceManager.PrivateLinks<MyPrivateLinkResource> {}
```

This generates all standard private endpoint and private link operations in one line.
