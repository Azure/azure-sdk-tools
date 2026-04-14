---
description: Key Vault-specific patterns, exceptions, and review heuristics for Python APIView reviews of azure-keyvault packages.
applyTo: "scratch/apiviews/python/**/*keyvault*.txt"
---

# Key Vault Python API Review — Service-Specific Patterns

Use this skill only when reviewing Python APIView text for Azure Key Vault libraries. Load it alongside the core API review and Python review skills when the package or namespace is under `azure.keyvault.*`, including secrets, keys, certificates, administration, and their `aio` variants.

These are approved patterns and known exceptions specific to the Azure Key Vault Python SDKs (`azure-keyvault-secrets`, `azure-keyvault-keys`, `azure-keyvault-certificates`, `azure-keyvault-administration`). They represent decisions made by Azure SDK architects through past API reviews.

## Approved Exceptions

### `vault_url` parameter name
Key Vault Python clients use `vault_url` as the first constructor parameter instead of the general `endpoint`. **This is an approved exception** to the standard naming guideline. Do NOT suggest renaming `vault_url` to `endpoint`.

```python
# CORRECT for Key Vault
class SecretClient:
    def __init__(self, vault_url: str, credential: TokenCredential, **kwargs) -> None
        ...

# Do NOT suggest this:
class SecretClient:
    def __init__(self, endpoint: str, credential: TokenCredential, **kwargs) -> None
        ...
```

### `vault_url` as a read-only property
Key Vault clients and identifier types expose `vault_url` as a read-only property. This is consistent across all Key Vault sub-packages. Do NOT suggest renaming to `endpoint` or `url`.

## Established Patterns

### Client inheritance from `KeyVaultClientBase`
All Key Vault clients inherit from an internal `KeyVaultClientBase`. This base class provides shared functionality (pipeline configuration, challenge-based authentication). Do NOT comment on this inheritance pattern.

### Soft-delete operations as LROs
`begin_delete_*` and `begin_recover_deleted_*` operations return `LROPoller` because Key Vault soft-delete and recovery are asynchronous server-side operations. These correctly use the `begin_` prefix pattern.

### Identifier classes
Key Vault provides `*Identifier` classes (e.g., `KeyVaultSecretIdentifier`, `KeyVaultKeyIdentifier`) that parse resource IDs into components (`vault_url`, `name`, `version`). These take a `source_id: str` as their sole constructor argument. This pattern is approved.

### `api_version` as keyword-only with default
Key Vault clients accept `api_version` as a keyword-only argument with a default pointing to the latest stable version. This allows pinning to a specific API version when needed.

### `verify_challenge_resource` parameter
Key Vault clients accept `verify_challenge_resource: Optional[bool]` to control whether the client verifies the authentication challenge resource matches the vault URL. This is a security feature specific to Key Vault's challenge-based authentication flow.

### Properties model separation
Key Vault separates full resource models (e.g., `KeyVaultSecret`) from properties-only models (e.g., `SecretProperties`). List operations return properties models (lighter weight), while get operations return full models. This is an established pattern — do NOT suggest combining them.

### `name` as primary identifier
Key Vault operations use `name: str` as the primary resource identifier rather than a full ID or URL. Version is a separate optional parameter where applicable.

## API Version Enum Conventions

Key Vault uses `ApiVersion(str, Enum)` with version strings like `V7_5 = "7.5"` and preview versions like `V7_6_PREVIEW_2 = "7.6-preview.2"`. The underscore-to-dot/hyphen mapping in enum names is standard for Key Vault.

## Common Review Points (NOT exceptions — these ARE issues if violated)

- `roll_secret` is a new operation (v4.11+) — review its parameter design against `set_secret` for consistency, especially around keyword-only optional arguments.
- Async Key Vault soft-delete and recovery operations should preserve the same long-running semantics and `begin_` naming pattern as the sync client.
- New public model classes (for example, `RollSecretParametersObject`) should not have the `Object` suffix — this is a valid naming concern.
- If a Key Vault secret API suddenly exposes unrelated storage or blob-style parameters, treat that as a likely generation or surface-shaping issue worth flagging.
