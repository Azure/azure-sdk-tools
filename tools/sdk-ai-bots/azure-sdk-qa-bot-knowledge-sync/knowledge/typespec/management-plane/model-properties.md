# ARM Model Properties and Schemas

How to define model properties, types, and constraints in TypeSpec and their Swagger mapping.

## How TypeSpec types map to Swagger types

| TypeSpec | Swagger `type` | Swagger `format` |
|----------|---------------|-----------------|
| `string` | `string` | — |
| `int32` | `integer` | `int32` |
| `int64` | `integer` | `int64` |
| `float32` | `number` | `float` |
| `float64` | `number` | `double` |
| `boolean` | `boolean` | — |
| `utcDateTime` | `string` | `date-time` |
| `plainDate` | `string` | `date` |
| `url` | `string` | `uri` |
| `bytes` | `string` | `byte` |

## How to define required vs optional and read-only properties

- Required field: `displayName: string;` → appears in `required` array
- Optional field: `age?: int32;` → not in `required`
- Read-only: `@visibility(Lifecycle.Read) createdAt?: utcDateTime;` → `"readOnly": true`
- Array: `emails: string[];` → `"type": "array", "items": { "type": "string" }`
- Dictionary: `metadata?: Record<string>;` → `"type": "object", "additionalProperties": { "type": "string" }`

## How to add property constraints (pattern, length, range)

```typespec
model EmployeeProperties {
  @pattern("^[a-zA-Z0-9-]+$")
  @minLength(3)
  @maxLength(24)
  name: string;

  @minValue(18)
  @maxValue(65)
  age: int32;
}
```

Swagger adds `pattern`, `minLength`, `maxLength`, `minimum`, `maximum` directly on the property.

## How to define an enum property

```typespec
union EmployeeStatus {
  string,
  Active: "Active",
  Inactive: "Inactive",
  OnLeave: "OnLeave",
}

model EmployeeProperties {
  status: EmployeeStatus;
}
```

Swagger: `"enum": ["Active", "Inactive", "OnLeave"]` with `"x-ms-enum": { "name": "EmployeeStatus", "modelAsString": true }`.

## How to define nested object properties

```typespec
model Address {
  street: string;
  city: string;
}
model EmployeeProperties {
  homeAddress: Address;
  workAddress?: Address;
}
```

Swagger: `homeAddress` references `$ref: "#/definitions/Address"` and appears in `required`. `workAddress` also references `Address` but is not in `required`.

## How to define provisioning state (standard ARM pattern)

Every ARM resource should include a provisioning state:

```typespec
model EmployeeProperties {
  @visibility(Lifecycle.Read)
  provisioningState?: ResourceProvisioningState;
}
```

Swagger: `"provisioningState"` with `readOnly: true`, enum `["Succeeded", "Failed", "Canceled"]`, `x-ms-enum` with `modelAsString: true`.

For custom states, extend the union:

```typespec
union MyProvisioningState {
  string,
  ResourceProvisioningState,
  Provisioning: "Provisioning",
  Updating: "Updating",
}
```
