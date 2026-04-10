---
description: Cross-language Azure SDK design principles for API surface review. Applies to all languages.
applyTo: "**"
---

# General Azure SDK Design Principles

These principles apply to all Azure SDK client libraries, regardless of language. They represent the cross-cutting design philosophy that guides API surface decisions.

## Client Design

### Service Client Pattern
- Each Azure service should have a dedicated **service client** class as the primary entry point.
- Service clients should be named `{ServiceName}Client` (e.g., `SecretClient`, `BlobClient`).
- Service clients take a service endpoint (URL) and a credential as their primary constructor arguments.
- Service clients should support both synchronous and asynchronous usage patterns appropriate for the language.

### Credential Handling
- All clients should accept `TokenCredential` (or language equivalent) for Azure Active Directory authentication.
- Credentials should be the second positional parameter after the endpoint URL.
- Clients should not store or log credentials.

### Configuration
- Optional configuration (retry policies, timeouts, logging) should be passed as keyword/optional arguments to the client constructor.
- Per-operation overrides should mirror constructor parameter names.

## Naming Conventions

### General Naming
- Use clear, descriptive names that convey purpose.
- Avoid abbreviations unless they are universally understood (e.g., URL, HTTP, ID).
- Be consistent — similar operations across clients should use similar names.
- Names should be appropriately concise. Avoid unnecessary suffixes (e.g., `Widget` not `WidgetObject`, `WidgetItem`, or `WidgetModel` unless disambiguation is necessary).

### Method Naming
- CRUD operations should follow consistent patterns: `create_*`, `get_*`, `update_*`, `delete_*`, `list_*`.
- Long-running operations (LROs) should follow the `begin_*` prefix convention.
- Boolean-returning methods should use `is_*` or `has_*` prefixes.

### Enum Values
- Enum types should use consistent casing per language conventions.
- Enum values should match their wire-format representations where applicable.

## API Usability

### Delight
- Does the API feel natural and intuitive to use?
- Are method names descriptive and self-documenting?
- Do parameters have clear purposes from their names?
- Is functionality discoverable through good naming?
- Are common operations easy and straightforward?

### Complexity
- Is the API more complex than necessary for its purpose?
- Are there too many parameters or methods?
- Could operations be simplified or combined?
- Is the hierarchy of classes clear and logical?
- Are abstractions at the right level?

### Consistency
- Are naming conventions consistent across the API?
- Do similar operations use similar naming patterns?
- Are abbreviations used consistently?
- Do names accurately reflect their purpose?
- Are names of reasonable length?

## Namespace Organization

- Namespaces should be hierarchical and reflect the service structure.
- Models (input/output types) should live in a `models` sub-namespace.
- Async variants should live in an `aio` (Python) or equivalent sub-namespace.

## Pagination

- List operations returning many results should use language-appropriate pagination patterns.
- Pagination should be transparent to the caller — iterating should automatically fetch pages.

## Long-Running Operations (LROs)

- Operations that take significant time should return a poller/operation object.
- The poller should provide status checking, waiting, and result retrieval.
- Method names for LROs should use a `begin_` prefix to signal asynchronous completion.

## Error Handling

- Errors should use language-appropriate exception types.
- Error messages should include the service error code and message.
- HTTP status codes should map to appropriate exception types.
