# Reference Document Links

## API Version Evolution

- [Versioning overview](https://azure.github.io/typespec-azure/docs/howtos/versioning/01-about-versioning/): Overview of how API versioning works in TypeSpec Azure.
- [preview → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/02-preview-after-preview/): How to add a new preview version after an existing preview version.
- [preview → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/): How to promote a preview version to stable.
- [stable → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/04-preview-after-stable/): How to add a new preview version after a stable version.
- [stable → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/05-stable-after-stable/): How to add a new stable version after an existing stable version.
- [Evolving APIs](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/): How to evolve your API across versions by adding, removing, or modifying resources, operations, and properties using versioning decorators.

## ARM Resource and Operation Authoring

Use these references when adding or changing ARM resources, child resources, CRUD/list operations, singleton or fixed-name resource paths, paging, and synchronous or long-running behavior. Prefer Azure Resource Manager resource and operation templates over hand-authored HTTP routes and responses.

- [ARM resource types](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/): Choose the appropriate tracked, proxy, tenant, extension, child, or singleton resource pattern; define `ResourceNameParameter`; model `provisioningState` with read visibility and `@lroStatus` when operations are long-running.
- [Defining child resource types](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step03/): Define a child with `@parentResource(Parent)`, its own resource-name parameter and path segment, and operations scoped to the parent.
- [ARM resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): Select standard `@armResourceOperations` templates for GET, PUT, PATCH, DELETE, list, HEAD, and actions; choose synchronous versus asynchronous templates according to actual service behavior. For lists, include the immediate-parent scope, plus subscription scope where required for tracked resources.
- [ARM interfaces and operation templates](https://azure.github.io/typespec-azure/docs/libraries/azure-resource-manager/reference/interfaces/): Look up the current signatures and generated response shapes for `ArmResourceRead`, create/replace, patch, delete, parent/scope/subscription list, and composite resource operation interfaces instead of guessing template parameters.
- [Singleton resource sample](https://azure.github.io/typespec-azure/docs/samples/resource-manager/resource-types/singleton/): Model a fixed-name resource path with `@singleton("default")` (or the contract's required fixed name) while retaining the standard resource model and `ResourceNameParameter`; do not replace the resource template with a manually assembled route.
- [ARM decorators reference](https://azure.github.io/typespec-azure/docs/libraries/azure-resource-manager/reference/decorators/): Reference `@armResourceOperations` routing behavior and its `allowStaticRoutes` escape hatch. Use static `@route` declarations only when a documented legacy or non-standard path cannot be represented by a standard resource or singleton template.
- [Paging operations](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/05pagingoperations/): For custom paged results, identify collection items with `@pageItems` and the continuation URL with `@nextLink`; avoid legacy paging decorators unless compatibility requires them. Standard ARM list templates already use the ARM resource-list result shape.
- [ARM RPC guidelines and TypeSpec linting coverage](https://azure.github.io/typespec-azure/docs/howtos/arm/rpc-guidelines-coverage/): Check which resource-path, CRUD, list, response-schema, provisioning-state, and LRO rules are enforced by templates or linting, and which runtime behaviors still require design review or integration tests.

When planning an ARM change, explicitly verify: the base resource type and parent relationship; the resource name and path segment; required CRUD/list operations for that resource kind; whether PUT, PATCH, DELETE, or actions are truly synchronous or require an async template; consistent resource response schemas across PUT/GET/PATCH/LIST; and whether paging and fixed-name behavior are represented by standard templates.

## Data-Plane Operations

- [Azure.Core reference](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference): Full reference for Azure.Core decorators, interfaces, operations, and models.
- [Standard resource operations](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/interfaces): Azure.Core operation templates (ResourceRead, ResourceList, ResourceCreateOrUpdate, ResourceDelete, etc.).
- [Data-plane getting started](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step01): Getting started guide for creating data-plane TypeSpec services with Azure.Core.
- [Deep Dive: Long-running (Asynchronous) Operations](https://azure.github.io/typespec-azure/docs/howtos/azure-core/long-running-operations/): Defining asynchronous (long-running) operations
