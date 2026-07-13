# ARM Extension Resources

How to define extension resources with multi-scope operations in TypeSpec.

## What is an extension resource and when to use it

An extension resource attaches to any existing ARM resource via `{resourceUri}`. Use it for cross-cutting concerns (locks, policies, diagnostics). Path: `/{resourceUri}/providers/Microsoft.Provider/extensionType/{name}`.

## How to define an extension resource model

```typespec
@extensionResource
model MyDiagnosticSetting is ExtensionResource<DiagnosticProperties> {
  @key("settingName")
  @path
  @segment("diagnosticSettings")
  name: string;
}
```

## How to generate full CRUD for an extension resource at all scopes

Use `ExtensionResourceOperations` to generate GET, PUT, PATCH, DELETE, and List at all scopes (tenant, subscription, resource group, parent resource).

```typespec
@armResourceOperations
interface DiagnosticSettings
  extends ExtensionResourceOperations<MyDiagnosticSetting, DiagnosticProperties> {}
```

Swagger generates paths for each scope:
- `/providers/Microsoft.P/diagnosticSettings/{name}` (tenant)
- `/subscriptions/{sub}/providers/Microsoft.P/diagnosticSettings/{name}` (subscription)
- `/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.P/diagnosticSettings/{name}` (resource group)
- `/{resourceUri}/providers/Microsoft.P/diagnosticSettings/{name}` (parent resource)

Plus corresponding list paths at each scope with pagination.

## How to define individual extension operations with a specific scope

```typespec
@armResourceOperations
interface DiagnosticSettings {
  get is Azure.ResourceManager.Extension.Read<
    Azure.ResourceManager.Extension.ResourceGroup, MyDiagnosticSetting
  >;
  create is Azure.ResourceManager.Extension.CreateOrReplaceAsync<
    Azure.ResourceManager.Extension.ResourceGroup, MyDiagnosticSetting
  >;
  delete is Azure.ResourceManager.Extension.DeleteAsync<
    Azure.ResourceManager.Extension.ResourceGroup, MyDiagnosticSetting
  >;
  list is Azure.ResourceManager.Extension.ListByTarget<
    Azure.ResourceManager.Extension.ResourceGroup, MyDiagnosticSetting
  >;
}
```

## What extension resource scope targets are available

| Target | TypeSpec Model | Path Prefix |
|--------|---------------|-------------|
| Resource Group | `Azure.ResourceManager.Extension.ResourceGroup` | `/subscriptions/{sub}/resourceGroups/{rg}` |
| Subscription | `Azure.ResourceManager.Extension.Subscription` | `/subscriptions/{sub}` |
| Tenant | `Azure.ResourceManager.Extension.Tenant` | `/` |
| Management Group | `Azure.ResourceManager.Extension.ManagementGroup` | `/providers/Microsoft.Management/managementGroups/{mgName}` |

## How to define an action on an extension resource

```typespec
op analyzeAsync is Azure.ResourceManager.Extension.ActionAsync<
  Azure.ResourceManager.Extension.ResourceGroup,
  MyDiagnosticSetting,
  AnalyzeRequest,
  AnalyzeResponse
>;
```

Swagger: POST at scope path + `/diagnosticSettings/{name}/analyzeAsync` with request body and LRO.
