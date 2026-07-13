# ARM Composite Interfaces (CRUD Shortcuts)

How to use composite interfaces to generate full CRUD operations with minimal code in TypeSpec.

## How TrackedResourceOperations generates all standard operations

One line generates GET, PUT (async), PATCH, DELETE (async), ListByResourceGroup, and ListBySubscription.

```typespec
model Employee is TrackedResource<EmployeeProperties> {
  ...ResourceNameParameter<Employee>;
}

@armResourceOperations
interface Employees
  extends TrackedResourceOperations<Employee, EmployeeProperties> {}
```

## How ProxyResourceOperations works for child resources

Generates GET, PUT (async), DELETE (async), and ListByParent. No PATCH or ListBySubscription.

```typespec
@parentResource(Employee)
model EmployeeRole is ProxyResource<RoleProperties> {
  ...ResourceNameParameter<EmployeeRole>;
}

@armResourceOperations
interface EmployeeRoles extends ProxyResourceOperations<EmployeeRole> {}
```

## How ExtensionResourceOperations works for multi-scope resources

Generates full CRUD at all scopes (tenant, subscription, resource group, parent resource).

```typespec
@armResourceOperations
interface Locks
  extends ExtensionResourceOperations<MyLock, LockProperties> {}
```

## Which composite interface generates which operations

| Interface | GET | PUT | PATCH | DELETE | ListByParent | ListBySub |
|-----------|-----|-----|-------|--------|-------------|-----------|
| `TrackedResourceOperations` | ✅ | ✅ async | ✅ | ✅ async | ✅ (by RG) | ✅ |
| `ProxyResourceOperations` | ✅ | ✅ async | — | ✅ async | ✅ | — |
| `ExtensionResourceOperations` | ✅ | ✅ async | ✅ | ✅ async | ✅ multi-scope | — |
| `ResourceInstanceOperations` | ✅ | ✅ async | ✅ | ✅ async | — | — |
| `ResourceCollectionOperations` | — | — | — | — | ✅ | — |
| `ResourceOperations` | ✅ | ✅ async | ✅ | ✅ async | ✅ | — |

## How to combine composite interfaces with custom operations

```typespec
@armResourceOperations
interface Employees
  extends TrackedResourceOperations<Employee, EmployeeProperties> {
  // Add custom actions on top of standard CRUD
  restart is ArmResourceActionSync<Employee, void, void>;
}
```

## How to build operations individually when composites don't fit

```typespec
@armResourceOperations
interface Employees {
  get is ArmResourceRead<Employee>;
  create is ArmResourceCreateOrReplaceAsync<Employee>;
  update is ArmCustomPatchSync<Employee, EmployeePatch>;
  delete is ArmResourceDeleteSync<Employee>;
  list is ArmResourceListByParent<Employee>;
}
```

This gives you full control over which operations to include and whether each is sync/async.
