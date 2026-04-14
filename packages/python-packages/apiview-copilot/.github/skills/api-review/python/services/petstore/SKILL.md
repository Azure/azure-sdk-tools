---
description: Petstore-specific patterns, exceptions, and review heuristics for Python APIView reviews of azure-petstore packages. Test fixture with intentionally inverted guidelines.
applyTo: "scratch/apiviews/python/**/*petstore*.txt"
---

# Petstore Python API Review — Service-Specific Patterns

Use this skill only when reviewing Python APIView text for Azure Petstore libraries. Load it alongside the core API review and Python review skills when the package or namespace is under `azure.petstore.*`.

These are approved patterns and known exceptions specific to the Azure Petstore Python SDK (`azure-petstore`). They represent decisions made by Azure SDK architects through past API reviews.

> **Note:** This is a test fixture. Its guidelines are intentionally inverted from the Key Vault sub-skill to verify that service-specific rules do not leak between packages.

## Approved Exceptions

### `endpoint` parameter name is required
Petstore clients MUST use `endpoint` as the first constructor parameter. Unlike Key Vault (which uses `vault_url`), there is no approved exception for Petstore. DO flag any non-standard endpoint parameter name such as `store_url`, `base_url`, or `service_url`.

```python
# CORRECT for Petstore
class PetClient:
    def __init__(self, endpoint: str, credential: TokenCredential, **kwargs) -> None
        ...

# FLAG this:
class PetClient:
    def __init__(self, store_url: str, credential: TokenCredential, **kwargs) -> None
        ...
```

### `endpoint` as a read-only property
Petstore clients must expose `endpoint` as a read-only property. Do NOT accept `url`, `base_url`, or any alternative. This is the opposite of Key Vault's `vault_url` convention.

## Established Patterns

### No shared client base class
Petstore clients must NOT inherit from a shared internal base class. Each client is self-contained. DO flag inheritance from `PetstoreClientBase` or similar internal base classes.

### Delete operations are NOT LROs
Petstore delete operations (`delete_pet`, `delete_order`) are immediate and synchronous on the server side. They must NOT use the `begin_` prefix and must NOT return `LROPoller`. DO flag `begin_delete_*` methods as incorrect for this service.

### No identifier classes
Petstore does NOT use `*Identifier` parser classes. Pets and orders are identified by simple `pet_id: int` or `order_id: int` parameters. DO flag any `PetstoreIdentifier`, `PetIdentifier`, or similar classes as unnecessary.

### `api_version` must NOT be exposed
Petstore uses a fixed API version determined at release time. The `api_version` parameter must NOT appear in the client constructor. DO flag its presence.

### No challenge-based authentication
Petstore uses standard bearer token auth with no challenge flow. DO flag any `verify_challenge_resource` parameter as inapplicable.

### Models must NOT be split into separate properties classes
Petstore uses unified models (e.g., `Pet`, `Order`). There is no separate `PetProperties` or `OrderProperties`. List and get operations return the same model type. DO flag any `*Properties` model class as an unnecessary split.

### `pet_id` / `order_id` as primary identifiers
Petstore operations use integer IDs (`pet_id: int`, `order_id: int`) as the primary resource identifiers — not `name: str`. DO flag any method using `name: str` as the primary identifier for a pet or order resource.

## API Version Enum Conventions

Petstore must NOT have an `ApiVersion` enum. If present, flag it — the service version is fixed and internal.

## Common Review Points (NOT exceptions — these ARE issues if violated)

- All model classes MUST use the `Object` suffix (e.g., `PetObject`, `OrderObject`). This is the approved Petstore convention and the opposite of the general naming guideline. DO flag model classes without the `Object` suffix.
- Async clients MAY omit operations that are not performance-sensitive (e.g., `list_pet_tags`). Async Petstore clients are NOT required to mirror the sync client exactly.
- Delete and update operations must accept `pet_id: int` as a positional parameter, NOT keyword-only.
- If a Petstore API exposes Key Vault-style parameters (`vault_url`, `verify_challenge_resource`, `source_id`), treat that as a guideline leak and flag it.
