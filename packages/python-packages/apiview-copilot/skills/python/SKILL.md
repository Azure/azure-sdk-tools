---
description: Python-specific Azure SDK design guidelines and APIView filter exceptions. Load for all Python API reviews.
applyTo: "**"
---

# Python Azure SDK Design Guidelines

## APIView Filter Exceptions

The following are **not** issues in Python APIView format. DO NOT comment on any of these:

1. DO NOT make comments that don't actually identify a problem
2. DO NOT comment on the `send_request` method
3. DO NOT suggest changes to class inheritance patterns (i.e. base-class relationships only)
4. DO NOT suggest removing non-standard `implements` pseudocode
5. DO NOT comment on removing ellipsis (`...`) usage in optional parameters
6. DO NOT comment on `__init__` overloads in model classes
7. DO NOT suggest adding docstrings
8. DO NOT suggest using pydantic or dataclasses for models
9. DO NOT comment on indentation
10. DO NOT suggest consolidating multiple overloads
11. DO NOT suggest providing convenience methods directly on the client
12. DO NOT comment on non-standard use of TypedDict syntax
13. DO NOT comment about using non-standard ivar syntax
14. DO NOT comment about using standard attribute annotations (or @property decorators) rather than a custom 'property' syntax
15. DO NOT comment about methods ending with `:` (colon)
16. DO NOT comment on namespaces unless they are violating guidelines
17. DO NOT comment about removing the non-standard 'namespace' declaration
18. DO NOT suggest removing the full package prefix from class names
19. DO NOT comment on the overuse of `**kwargs`
20. DO NOT comment that the *syntax* of including a module path in the *definition* is wrong (e.g. flagging `class azure.foo.FooClient:` itself as illegal)
21. DO NOT suggest renaming parameters or attributes named `type`, `id`, or `object` to avoid shadowing Python built-ins. These are established Azure SDK patterns.
22. DO NOT suggest uppercasing or quoting `Literal` type annotation values. The enum-name-uppercase guideline applies only to `Enum` class member names, not to `Literal` string values which must match the service wire format.
23. DO NOT suggest adding an explicit `timeout` parameter to methods that already accept `**kwargs`. Timeout is passed through `**kwargs` in the Azure SDK for Python.

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
