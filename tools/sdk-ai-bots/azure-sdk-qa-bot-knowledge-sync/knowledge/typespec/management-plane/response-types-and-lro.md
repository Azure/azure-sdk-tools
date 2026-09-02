# ARM Response Types and Long-Running Operations (LRO)

How to configure response status codes, LRO headers, and polling patterns in TypeSpec.

## What ARM response types are available and their status codes

| TypeSpec Model | Code | Has Body? | Use Case |
|---------------|------|-----------|----------|
| `ArmResponse<T>` | 200 | Yes | Successful sync operation |
| `ArmResourceCreatedResponse<T>` | 201 | Yes + LRO headers | Async resource creation |
| `ArmResourceCreatedSyncResponse<T>` | 201 | Yes | Sync resource creation |
| `ArmResourceUpdatedResponse<T>` | 200 | Yes | Resource updated |
| `ArmAcceptedLroResponse` | 202 | No, LRO headers | Async operation accepted |
| `ArmAcceptedResponse` | 202 | No | Sync accepted (no LRO) |
| `ArmDeletedResponse` | 200 | No | Resource deleted |
| `ArmDeleteAcceptedLroResponse` | 202 | No, LRO headers | Async delete accepted |
| `ArmDeletedNoContentResponse` | 204 | No | Resource doesn't exist |
| `ArmNoContentResponse` | 204 | No | No content |
| `ArmResourceExistsResponse` | 200 | No | HEAD - exists |

## How Azure-AsyncOperation header polling works (default LRO pattern)

Most ARM async operations use `Azure-AsyncOperation` header by default.

```typespec
op create is ArmResourceCreateOrReplaceAsync<Employee>;
```

Swagger includes `x-ms-long-running-operation: true` with `final-state-via: azure-async-operation`. The `201` response has `Azure-AsyncOperation` and `Retry-After` headers.

## How to use Location header polling instead of Azure-AsyncOperation

Use `ArmLroLocationHeader` for Location-based polling.

```typespec
op create is ArmResourceCreateOrReplaceAsync<
  Employee,
  Response = ArmResponse<Employee> | ArmResourceCreatedResponse<
    Employee,
    LroHeaders = ArmLroLocationHeader<FinalResult = Employee>
  >
>;
```

Swagger: `final-state-via: location` with `Location` and `Retry-After` headers.

## How to use both Azure-AsyncOperation and Location headers (combined)

Use `ArmCombinedLroHeaders` for dual-header support.

```typespec
op delete is ArmResourceDeleteWithoutOkAsync<
  Employee,
  LroHeaders = ArmCombinedLroHeaders<ArmOperationStatus, Employee>
>;
```

Swagger: `202` response includes both `Azure-AsyncOperation` and `Location` headers.

## Which LRO header type to choose

| TypeSpec LRO Header | Headers Generated | `final-state-via` |
|---------------------|-------------------|-------------------|
| (default) | `Azure-AsyncOperation` | `azure-async-operation` |
| `ArmLroLocationHeader` | `Location` + `Retry-After` | `location` |
| `ArmCombinedLroHeaders` | Both | `azure-async-operation` |

## How to add custom headers to a response

```typespec
@post
op create(...ResourceInstanceParameters<Employee>): ArmCreatedResponse<
  Employee,
  ExtraHeaders = {
    @header("x-ms-client-request-id") clientRequestId: string;
  }
>;
```

Swagger: `201` response includes the custom header in the `headers` section.

## How the ArmOperationStatus model works for polling endpoints

```typespec
model MyOperationStatus extends ArmOperationStatus {
  properties: Record<string>;
}
```

Swagger generates a model with `id`, `name`, `status`, `percentComplete`, `startTime`, `endTime`, `error` (all readOnly), plus your custom `properties`.

## How error responses work in ARM operations

All ARM operations include a `default` error response referencing `ErrorResponse` from common-types. The `ErrorResponse` contains `ErrorDetail` with `code`, `message`, `target`, `details[]`, and `additionalInfo[]`.
