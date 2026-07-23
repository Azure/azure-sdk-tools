# ARM CRUD Operations

How to define create, read, update, and delete operations for ARM resources in TypeSpec.

## How to define an async create (PUT) operation with LRO

Use `ArmResourceCreateOrReplaceAsync` for the most common create pattern. Client gets 201 with polling headers.

```typespec
@armResourceOperations
interface Employees {
  createOrUpdate is ArmResourceCreateOrReplaceAsync<Employee>;
}
```

Swagger: PUT with `200` (updated) and `201` (created) responses, both returning the resource. Includes `x-ms-long-running-operation: true` with `final-state-via: azure-async-operation`.

## How to define a sync create (PUT) operation without LRO

Use `ArmResourceCreateOrReplaceSync` when creation completes immediately.

```typespec
@armResourceOperations
interface Employees {
  createOrUpdate is ArmResourceCreateOrReplaceSync<Employee>;
}
```

Swagger: Same as async but without `x-ms-long-running-operation`. Returns `200`/`201` with the resource body.

## How to define a create operation with Location header polling

Use `ArmLroLocationHeader` instead of the default Azure-AsyncOperation header.

```typespec
@armResourceOperations
interface Employees {
  createOrUpdate is ArmResourceCreateOrReplaceAsync<
    Employee,
    Response = ArmResponse<Employee> | ArmResourceCreatedResponse<
      Employee,
      LroHeaders = ArmLroLocationHeader<FinalResult = Employee>
    >
  >;
}
```

Swagger: `201` response includes `Location` and `Retry-After` headers, with `final-state-via: location`.

## How to define a GET operation to read a single resource

Use `ArmResourceRead` for a standard GET by resource name.

```typespec
@armResourceOperations
interface Employees {
  get is ArmResourceRead<Employee>;
}
```

Swagger: GET at `.../employees/{employeeName}` with `200` response returning the resource and `default` error response.

## How to define a HEAD operation to check resource existence

Use `ArmResourceCheckExistence` for a lightweight existence check.

```typespec
@armResourceOperations
interface Employees {
  checkExistence is ArmResourceCheckExistence<Employee>;
}
```

Swagger: HEAD with `200` (exists) and `404` (not found) responses, no response body.

## How to define a standard update (PATCH) with auto-generated patch model

Use `ArmResourcePatchAsync` — the patch body is auto-derived from properties (all fields optional).

```typespec
@armResourceOperations
interface Employees {
  update is ArmResourcePatchAsync<Employee, EmployeeProperties>;
}
```

Swagger: PATCH with auto-generated `EmployeeUpdate` model (all properties optional), `200` + `202` responses, LRO enabled.

## How to define a tags-only update (PATCH)

Use `ArmTagsPatchSync` or `ArmTagsPatchAsync` to update only the `tags` field.

```typespec
@armResourceOperations
interface Employees {
  update is ArmTagsPatchSync<Employee>;
}
```

Swagger: PATCH body is `TagsUpdateModel` with only `tags` property.

## How to define an update (PATCH) with a custom patch body

Use `ArmCustomPatchAsync` or `ArmCustomPatchSync` with your own patch model.

```typespec
model MyCustomPatch {
  city?: string;
  priority?: int32;
}

@armResourceOperations
interface Employees {
  update is ArmCustomPatchAsync<Employee, MyCustomPatch>;
}
```

Swagger: PATCH with your custom model as the body, `200`/`202` responses, LRO with `azure-async-operation`.

## How to define an async delete operation (recommended pattern)

Use `ArmResourceDeleteWithoutOkAsync`. Returns `202` (accepted, use polling) and `204` (not found).

```typespec
@armResourceOperations
interface Employees {
  delete is ArmResourceDeleteWithoutOkAsync<Employee>;
}
```

Swagger: DELETE with `202` (LRO headers: `Azure-AsyncOperation`, `Location`, `Retry-After`) and `204`. Includes `x-ms-long-running-operation: true`.

## How to define a sync delete operation

Use `ArmResourceDeleteSync` when deletion completes immediately.

```typespec
@armResourceOperations
interface Employees {
  delete is ArmResourceDeleteSync<Employee>;
}
```

Swagger: DELETE with `200` (deleted) and `204` (not found), no LRO.

## Which create operation template to choose

| Scenario | Template | LRO? |
|----------|----------|-------|
| Standard async create/replace | `ArmResourceCreateOrReplaceAsync` | Yes |
| Sync create/replace | `ArmResourceCreateOrReplaceSync` | No |
| Async create/update (deprecated) | `ArmResourceCreateOrUpdateAsync` | Yes |

## Which update operation template to choose

| Scenario | Template | LRO? |
|----------|----------|-------|
| Auto-generated patch (async) | `ArmResourcePatchAsync` | Yes |
| Auto-generated patch (sync) | `ArmResourcePatchSync` | No |
| Tags-only (async/sync) | `ArmTagsPatchAsync` / `ArmTagsPatchSync` | Yes/No |
| Custom patch body (async/sync) | `ArmCustomPatchAsync` / `ArmCustomPatchSync` | Yes/No |

## Which delete operation template to choose

| Scenario | Template | LRO? | Status Codes |
|----------|----------|-------|-------------|
| Async delete (recommended) | `ArmResourceDeleteWithoutOkAsync` | Yes | 202, 204 |
| Sync delete | `ArmResourceDeleteSync` | No | 200, 204 |
| Async delete (deprecated) | `ArmResourceDeleteAsync` | Yes | 200, 202, 204 |
