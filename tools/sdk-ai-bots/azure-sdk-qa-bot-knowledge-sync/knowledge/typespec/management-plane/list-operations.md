# ARM List Operations

How to define list/collection operations for ARM resources in TypeSpec.

## How to list resources by resource group

Use `ArmResourceListByParent` for the most common list pattern. Returns paginated results with `nextLink`.

```typespec
@armResourceOperations
interface Employees {
  listByResourceGroup is ArmResourceListByParent<Employee>;
}
```

Swagger: GET at `.../providers/Microsoft.Provider/employees` with `x-ms-pageable: { nextLinkName: "nextLink" }`. Response schema is `EmployeeListResult` with `value` array and optional `nextLink`.

## How to list resources by subscription

Use `ArmListBySubscription` to list across all resource groups.

```typespec
@armResourceOperations
interface Employees {
  listBySubscription is ArmListBySubscription<Employee>;
}
```

Swagger: GET at `/subscriptions/{sub}/providers/Microsoft.Provider/employees` with pagination.

## How to list child resources by parent

For nested/proxy resources, `ArmResourceListByParent` automatically scopes to the parent.

```typespec
@parentResource(Employee)
model EmployeeRole is ProxyResource<RoleProperties> {
  ...ResourceNameParameter<EmployeeRole>;
}

@armResourceOperations
interface EmployeeRoles {
  list is ArmResourceListByParent<EmployeeRole>;
}
```

Swagger: GET at `.../employees/{employeeName}/employeeRoles` with pagination.

## How to define the required operations list endpoint

Every ARM service must expose `GET /providers/Microsoft.Provider/operations`:

```typespec
interface Operations extends Azure.ResourceManager.Operations {}
```

Swagger: GET returning `OperationListResult` with `value` array of `Operation` objects (name, isDataAction, display) and `nextLink`.

## How pagination works in ARM list operations

All list operations generate the standard ARM pagination pattern:
- Response includes `value` array and optional `nextLink` URI
- Swagger includes `x-ms-pageable` with `"nextLinkName": "nextLink"`
- Client follows `nextLink` to fetch the next page

The auto-generated list result model:

```json
{
  "EmployeeListResult": {
    "properties": {
      "value": { "type": "array", "items": { "$ref": "#/definitions/Employee" } },
      "nextLink": { "type": "string", "format": "uri" }
    },
    "required": ["value"]
  }
}
```

## How to list extension resources at multiple scopes

Use `ExtensionResourceCollectionOperations` to generate list operations at all scopes (tenant, subscription, resource group, parent resource).

```typespec
@armResourceOperations
interface MyExtensions
  extends ExtensionResourceCollectionOperations<MyExtension> {}
```

Swagger generates separate list paths for each scope, all with pagination.
