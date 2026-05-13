---
description: Python-specific Azure SDK design guidelines and APIView filter exceptions. Supporting skill for Python API reviews only.
applyTo: ""
---

# Python Azure SDK Design Guidelines

Use this skill only when reviewing a Python SDK API surface in APIView format. Do not use it for general Python coding or implementation tasks.

## Python APIView Conventions

Python APIView uses non-standard pseudocode to represent API surfaces. The following are **display conventions**, not code issues — do not comment on any of them:

- A `namespace` declaration denotes package structure.
- Classes include their full namespace in their definition: `class azure.contoso.ClassName` where `azure.contoso` is the namespace and `ClassName` is the class name.
- Indentation and structure convey scope (classes, methods, properties).
- Ellipsis (`...`) in optional parameters is a default-value placeholder.
- `implements` pseudocode expresses interface implementation.
- TypedDict syntax may be non-standard.
- Instance variables use a non-standard `ivar` syntax.
- Properties use a custom `property` syntax rather than standard `@property` decorators or attribute annotations.
- Method signatures will not end with `:` (colon) because they don't contain an implementation.

## APIView Filter Exceptions

The following are **not** issues in Python APIView. DO NOT comment on any of these:

1. DO NOT comment on the `send_request` method.
2. DO NOT suggest changes to class inheritance patterns (i.e. base-class relationships only).
3. DO NOT comment on `__init__` overloads in model classes.
4. DO NOT suggest adding docstrings.
5. DO NOT suggest using pydantic or dataclasses for models.
6. DO NOT suggest consolidating multiple overloads.
7. DO NOT suggest providing convenience methods directly on the client.
8. DO NOT comment on namespaces unless they are violating guidelines.
9. DO NOT comment on the overuse of `**kwargs`.
10. DO NOT suggest renaming parameters or attributes named `type`, `id`, or `object` to avoid shadowing Python built-ins. These are established Azure SDK patterns.
11. DO NOT suggest uppercasing or quoting `Literal` type annotation values. The enum-name-uppercase guideline applies only to `Enum` class member names, not to `Literal` string values which must match the service wire format.
12. DO NOT suggest adding an explicit `timeout` parameter to methods that already accept `**kwargs`. Timeout is passed through `**kwargs` in the Azure SDK for Python.

## Pythonic Design Evaluation

Beyond specific guidelines, evaluate Python APIs against these design principles:
- Does it follow "The Zen of Python" principles?
- Are Python idioms used appropriately?
- Does it leverage Python's strengths (e.g. duck typing, iterators)?
- Does it avoid un-Pythonic patterns from other languages?
- Is it consistent with the Python standard library style?
- Does the API use proper type hints and follow best static typing practices for Python?

## Client Constructor Design

### Endpoint and Credential
- Clients take the endpoint URL as the first positional argument.
- `TokenCredential` (from `azure.core.credentials`) is the second positional argument.
- Additional configuration is passed as keyword-only arguments via `**kwargs`.

### Policy Configuration
- DO accept optional default request options as keyword arguments and pass them along to pipeline policies.
- DO provide keyword-only arguments that override per-request policy options. Parameter names MUST mirror the names used in the client constructor.

### Async Clients
- Async clients must live in a `.aio` sub-namespace (e.g., `azure.keyvault.secrets.aio`).
- Async clients should have the same name as their sync counterpart (e.g., both are `SecretClient`).
- Async clients must implement `async __aenter__` and `async __aexit__` for context manager support.

## Models

### Constructor Design
- DO craft constructors for user-instantiated models with minimal required arguments and optional arguments as keyword-only.
- Model classes should not have a `Class` suffix — use `Widget` not `WidgetClass`.
- Model classes should not have a `Model` suffix unless disambiguation is necessary.

### Enums
- DO use `UPPERCASE` names for enum members.
- Enum classes should inherit from `str, Enum` for string-valued enums.
- Enum member values should match the service wire format.

```python
# GOOD
class VisualFeatures(str, Enum):
    CAPTION = 'caption'
    DENSE_CAPTIONS = 'denseCaptions'

# BAD
class VisualFeatures(str, Enum):
    Caption = 'caption'     # PascalCase — should be UPPERCASE
    dense_captions = 'denseCaptions'  # lowercase — should be UPPERCASE
```

## Method Signatures

### Keyword-Only Arguments
- DO use keyword-only arguments for optional or less-often-used arguments.
- Required keyword-only arguments are acceptable.

```python
# GOOD
def foo(a, b, *, c, d=None):
    ...

# BAD
def foo(a, b, c=None, d=None):
    ...  # optional args should be keyword-only
```

### Positional Parameters
- DO specify parameter names when calling methods with more than two required positional parameters.

### Method Naming
- Operations should follow consistent CRUD patterns: `create_*`, `get_*`, `update_*`, `delete_*`, `list_*`.
- Long-running operations use `begin_*` prefix.
- Methods returning `bool` should use `is_*` or `has_*`.

## Type Hints

- All public methods and properties must have type annotations.
- Use `Optional[T]` for nullable parameters.
- Use `Union[...]` sparingly — prefer overloads for complex signatures.
- Return types must be annotated.

## Namespace Structure

Expected structure for a Python SDK package:
```
namespace azure.{service}
  - {Service}Client (sync client)

namespace azure.{service}.aio
  - {Service}Client (async client)

namespace azure.{service}.models
  - model classes and enums
```

## Pagination

- List operations should return `ItemPaged[T]` (sync) or `AsyncItemPaged[T]` (async).
- The paged type handles fetching across pages transparently.

## Long-Running Operations

- LRO methods use the `begin_` prefix and return `LROPoller[T]` (sync) or `AsyncLROPoller[T]` (async).
