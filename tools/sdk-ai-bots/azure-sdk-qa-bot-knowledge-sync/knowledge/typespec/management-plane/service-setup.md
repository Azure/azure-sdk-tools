# ARM Service Setup and Decorators

How to set up an ARM TypeSpec project, configure namespaces, versioning, and key decorators.

## How to set up a minimal ARM TypeSpec service

Every ARM TypeSpec project needs a namespace with `@armProviderNamespace`, `@service`, versioning, and an Operations interface.

```typespec
import "@azure-tools/typespec-azure-resource-manager";
import "@typespec/versioning";

using Azure.ResourceManager;
using TypeSpec.Versioning;

@armProviderNamespace
@service(#{ title: "ContosoProviderHubClient" })
@versioned(Versions)
namespace Microsoft.ContosoProviderHub;

enum Versions {
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
  `2024-01-01`,
}

interface Operations extends Azure.ResourceManager.Operations {}
```

Swagger generates: `swagger: "2.0"`, `host: "management.azure.com"`, OAuth2 security, and the operations list endpoint.

## How @armProviderNamespace sets the provider name

Defaults to the namespace name. Can be overridden:

```typespec
@armProviderNamespace
namespace Microsoft.Contoso;  // provider = Microsoft.Contoso

@armProviderNamespace("Microsoft.CustomName")
namespace Microsoft.Contoso;  // provider = Microsoft.CustomName
```

## How @armCommonTypesVersion controls common-types $ref version

This sets which version of ARM common-types is used in Swagger `$ref` paths (e.g., `v3`, `v5`).

```typespec
enum Versions {
  @armCommonTypesVersion(Azure.ResourceManager.CommonTypes.Versions.v5)
  `2024-01-01`,
}
```

Affects all `$ref` paths: `common-types/resource-management/v5/types.json`.

## How @armLibraryNamespace creates reusable type libraries

Marks a namespace as a shared library. Use with `@useLibraryNamespace` in consuming services.

```typespec
@armLibraryNamespace
namespace Microsoft.MyServiceLibrary {
  model SharedConfig { name: string; value: string; }
}

// In consuming service:
@useLibraryNamespace(Microsoft.MyServiceLibrary)
namespace Microsoft.ConsumingService;
```

## What resource type decorators are available

| Decorator | Purpose | Path Scope |
|-----------|---------|-----------|
| `@tenantResource` | Tenant-scoped | `/providers/Microsoft.P/...` |
| `@subscriptionResource` | Subscription-scoped | `/subscriptions/{sub}/providers/Microsoft.P/...` |
| `@locationResource` | Location-scoped | `.../locations/{location}/...` |
| `@extensionResource` | Any parent (uses resourceUri) | `/{resourceUri}/providers/Microsoft.P/...` |
| `@singleton` | Single instance | `.../resourceType/default` |
| `@parentResource(Parent)` | Nested under parent | `.../parentType/{name}/childType/{name}` |

## How @armResourceOperations works on interfaces

Required on interfaces containing resource operations. Automatically adds `@autoRoute` and `@tag`.

```typespec
@armResourceOperations
interface Employees {
  get is ArmResourceRead<Employee>;
  create is ArmResourceCreateOrReplaceAsync<Employee>;
}
```

Options: `allowStaticRoutes` (allow `@route` on operations), `resourceType` (explicit resource), `omitTags` (disable auto-tagging).
