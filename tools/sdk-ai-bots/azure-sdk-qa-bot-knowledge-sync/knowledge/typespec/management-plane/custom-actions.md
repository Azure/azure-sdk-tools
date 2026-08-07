# ARM Custom Actions (POST Operations)

How to define custom POST actions on ARM resources in TypeSpec.

## How to define a sync resource action with request and response body

Use `ArmResourceActionSync` for actions that complete immediately (e.g., move, validate, restart).

```typespec
model MoveRequest {
  from: string;
  to: string;
}
model MoveResponse {
  status: string;
}

@armResourceOperations
interface Employees {
  move is ArmResourceActionSync<Employee, MoveRequest, MoveResponse>;
}
```

Swagger: POST at `.../employees/{employeeName}/move` with request body and `200` response containing `MoveResponse`.

## How to define an async resource action with LRO

Use `ArmResourceActionAsync` for actions that take time to complete.

```typespec
@armResourceOperations
interface Employees {
  startMigration is ArmResourceActionAsync<
    Employee, MigrationRequest, MigrationResponse
  >;
}
```

Swagger: POST with `200` (response body) + `202` (accepted), `x-ms-long-running-operation: true`.

## How to define a resource action that returns no content (204)

Use `ArmResourceActionNoContentSync` for actions that return empty body.

```typespec
@armResourceOperations
interface Employees {
  reset is ArmResourceActionNoContentSync<Employee, ResetRequest>;
}
```

Swagger: POST with `204` (No Content) response only.

## How to define a provider-level action (not scoped to a resource instance)

Use `ArmProviderActionSync` or `ArmProviderActionAsync` with a scope parameter.

```typespec
interface ProviderActions {
  validateConfig is ArmProviderActionSync<
    ValidateRequest, ValidateResponse, SubscriptionActionScope
  >;
}
```

Swagger: POST at `/subscriptions/{sub}/providers/Microsoft.Provider/validateConfig`.

Scope options:
- `SubscriptionActionScope` → `/subscriptions/{sub}/providers/...`
- `TenantActionScope` → `/providers/...`
- `ExtensionActionScope` → `/{resourceUri}/providers/...`

## How to define a check name availability operation

Use the built-in `checkGlobalNameAvailability` or `checkLocalNameAvailability`.

```typespec
@armResourceOperations
interface Operations {
  checkGlobal is checkGlobalNameAvailability;
  // or location-scoped:
  checkLocal is checkLocalNameAvailability;
}
```

Swagger: POST to `.../checkNameAvailability` with `CheckNameAvailabilityRequest` body (`name`, `type`) and `CheckNameAvailabilityResponse` response (`nameAvailable`, `reason`, `message`).

## How to define a custom action using the @armResourceAction decorator

For full control over a POST operation while keeping ARM-compliant routing:

```typespec
@autoRoute
@armResourceAction(Employee)
@post
op moveEmployee(
  ...ResourceInstanceParameters<Employee>,
  @body request: MoveRequest,
): MoveResponse | ErrorResponse;
```

Swagger: POST at `.../employees/{employeeName}/moveEmployee` with the specified body and response.

## Which action template to choose

| Scenario | Template | LRO? | Response |
|----------|----------|-------|---------|
| Sync with response | `ArmResourceActionSync` | No | 200 + body |
| Async with response | `ArmResourceActionAsync` | Yes | 200 + body, 202 |
| Sync no content | `ArmResourceActionNoContentSync` | No | 204 |
| Async no content | `ArmResourceActionNoContentAsync` | Yes | 202, 204 |
| Async no response body | `ArmResourceActionNoResponseContentAsync` | Yes | 200 (empty), 202 |
| Provider-level sync | `ArmProviderActionSync` | No | 200 + body |
| Provider-level async | `ArmProviderActionAsync` | Yes | 200, 202 |
