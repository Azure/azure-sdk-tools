# ARM Resource Types

How to define ARM resource models in TypeSpec and their Swagger output.

## How to define a tracked resource (top-level resource with location and tags)

A `TrackedResource` is a top-level ARM resource in a resource group with `location` and `tags`. Use it for primary resources (VMs, databases, storage accounts).

```typespec
model Employee is TrackedResource<EmployeeProperties> {
  ...ResourceNameParameter<Employee>;
}

model EmployeeProperties {
  age?: int32;
  city?: string;
  @visibility(Lifecycle.Read)
  provisioningState?: ResourceProvisioningState;
}
```

Swagger generates a definition with `allOf` referencing `TrackedResource`, containing read-only `id`, `name`, `type`, `systemData`, plus a `properties` object. Path: `/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Provider/employees/{employeeName}`.

## How to define a proxy resource (child or nested resource without location)

A `ProxyResource` has no `location` or `tags`. Use it for child/nested resources under a parent, e.g., a "FirewallRule" under a "Server".

```typespec
@parentResource(Employee)
model EmployeeRole is ProxyResource<RoleProperties> {
  ...ResourceNameParameter<EmployeeRole>;
}
```

Path: `.../employees/{employeeName}/employeeRoles/{employeeRoleName}`.

## How to define an extension resource (attaches to any ARM resource)

An `ExtensionResource` attaches to any existing ARM resource via `{resourceUri}`. Use it for cross-cutting concerns like locks, policies, diagnostics.

```typespec
@extensionResource
model DiagnosticSetting is ExtensionResource<DiagnosticProperties> {
  @key("diagnosticName")
  @path
  @segment("diagnosticSettings")
  name: string;
}
```

Swagger generates paths for multiple scopes (tenant, subscription, resourceGroup, parent resource) using `{resourceUri}` parameter.

## How to define a singleton resource (only one instance exists)

A singleton resource has exactly one instance under its parent, with no name parameter in the URL (uses a fixed value like "default").

```typespec
@singleton
@parentResource(Employee)
model EmployeeSettings is ProxyResource<SettingsProperties> {
  ...ResourceNameParameter<EmployeeSettings, "default">;
}
```

Path: `.../employees/{employeeName}/settings/default`.

## How to define a tenant-scoped resource

Use `@tenantResource` for resources at tenant scope. Path: `/providers/Microsoft.Provider/resourceType/{name}`.

```typespec
@tenantResource
model TenantConfig is ProxyResource<TenantConfigProperties> {
  ...ResourceNameParameter<TenantConfig>;
}
```

## How to define a subscription-scoped resource

Use `@subscriptionResource`. Path: `/subscriptions/{sub}/providers/Microsoft.Provider/resourceType/{name}`.

```typespec
@subscriptionResource
model SubscriptionQuota is ProxyResource<QuotaProperties> {
  ...ResourceNameParameter<SubscriptionQuota>;
}
```

## How to define a location-scoped resource

Path: `.../providers/Microsoft.Provider/locations/{location}/resourceType/{name}`.

```typespec
@parentResource(ArmLocationResource<"ResourceGroup">)
model RegionalConfig is TrackedResource<RegionalConfigProperties> {
  ...ResourceNameParameter<RegionalConfig>;
}
```

## How to add standard envelope properties (identity, SKU, plan, encryption, etc.)

Spread these into your resource model to add standard ARM properties:

- `...ManagedServiceIdentityProperty` → `identity` with SystemAssigned + UserAssigned
- `...ManagedSystemAssignedIdentityProperty` → `identity` with SystemAssigned only
- `...ResourceSkuProperty` → `sku` with name, tier, size, family, capacity
- `...ResourcePlanProperty` → `plan` with name, publisher, product
- `...ResourceKindProperty` → `kind` string
- `...EntityTagProperty` → `etag` (readOnly)
- `...ExtendedLocationProperty` → `extendedLocation` with name and type
- `...ManagedByProperty` → `managedBy` string
- `...AvailabilityZonesProperty` → `zones` array of strings
- `...DefaultProvisioningStateProperty` → `provisioningState` (readOnly)

Example with identity and SKU:

```typespec
model MyVm is TrackedResource<VmProperties> {
  ...ResourceNameParameter<MyVm>;
  ...ManagedServiceIdentityProperty;
  ...ResourceSkuProperty;
}
```

## How ResourceNameParameter generates the name path parameter

`ResourceNameParameter<T>` generates the `@key`, `@path`, and `@segment` for a resource name.

```typespec
model Employee is TrackedResource<EmployeeProperties> {
  ...ResourceNameParameter<Employee>;
}
```

Swagger output:

```json
{
  "name": "employeeName",
  "in": "path",
  "required": true,
  "type": "string",
  "pattern": "^[a-zA-Z0-9-]{3,24}$"
}
```
