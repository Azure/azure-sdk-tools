# ARM Parameters and Resource Scoping

How to define parameters and scope ARM resources in TypeSpec.

## What standard ARM parameters are auto-included

These parameters are automatically added by ARM operation templates — you don't define them manually:

| Parameter | Type | Location | Swagger $ref |
|-----------|------|----------|-------------|
| `api-version` | string | query | `ApiVersionParameter` |
| `subscriptionId` | uuid | path | `SubscriptionIdParameter` |
| `resourceGroupName` | string | path | `ResourceGroupNameParameter` |
| `location` | string | path | `LocationParameter` |

## How ResourceInstanceParameters provides all path parameters for a resource

`ResourceInstanceParameters<T>` includes subscriptionId, resourceGroupName, api-version, and the resource name parameter.

```typespec
op get(...ResourceInstanceParameters<Employee>): Employee | ErrorResponse;
```

Swagger generates all standard path parameters plus `employeeName` in the `parameters` array.

## What base parameter models exist for different scopes

| Scope | Base Parameters | Path Prefix |
|-------|----------------|-------------|
| Resource Group | `ResourceGroupBaseParameters` | `/subscriptions/{sub}/resourceGroups/{rg}` |
| Subscription | `SubscriptionBaseParameters` | `/subscriptions/{sub}` |
| Tenant | `TenantBaseParameters` | `/` |
| Location | `LocationBaseParameters` | `.../locations/{location}` |
| Extension | `ExtensionBaseParameters` | `/{resourceUri}` |

## What scope templates exist for provider-level actions

| Scope | Template | Path |
|-------|----------|------|
| Subscription | `SubscriptionActionScope` | `/subscriptions/{sub}/providers/Microsoft.P/action` |
| Tenant | `TenantActionScope` | `/providers/Microsoft.P/action` |
| Extension | `ExtensionActionScope` | `/{resourceUri}/providers/Microsoft.P/action` |
| Resource Group | `ResourceGroupScope` | `/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.P/action` |
| Location | `LocationScope` | `.../locations/{location}/action` |

## How to define a custom resource name path parameter with constraints

```typespec
model Employee is TrackedResource<EmployeeProperties> {
  @pattern("^[a-zA-Z0-9-]{3,24}$")
  @key("employeeName")
  @path
  @segment("employees")
  name: string;
}
```

Swagger: `{ "name": "employeeName", "in": "path", "required": true, "type": "string", "pattern": "^[a-zA-Z0-9-]{3,24}$" }`

## How to add custom query parameters to a list operation

```typespec
@armResourceOperations
interface Employees {
  list is ArmResourceListByParent<
    Employee,
    Parameters = {
      @query("$filter") filter?: string;
      @query("$top") top?: int32;
    }
  >;
}
```

Swagger adds `$filter` (string) and `$top` (integer) as query parameters.

## How extension resources use the resourceUri parameter

Extension resources use `{resourceUri}` with `x-ms-skip-url-encoding: true` to attach to any ARM resource.

Swagger: `{ "name": "resourceUri", "in": "path", "required": true, "type": "string", "x-ms-skip-url-encoding": true }`
