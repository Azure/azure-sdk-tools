# Secret Stores

The services that create, store and use the secrets are called secret stores in the rotation tool.  A store may act as Origin and create new secret values, as Primary and store the secret value along with rotation metadata like expiration date and revocation date, or it can act as a secondary store that gets updated with the new secret values on rotation.

All of the stores inherit from the abstract class [SecretStore](../Azure.Sdk.Tools.SecretRotation.Core/SecretStore.cs).

# Capabilities
The `SecretStore` class has methods categorized into 5 capabilities which are used during the rotation process:

## CanRead

Stores implementing `CanRead` must override the methods:
 - `GetCurrentStateAsync`
 - `GetRotationArtifactsAsync`

## CanWrite
Stores implementing `CanWrite` must override the methods:
 - `WriteSecretAsync`

## CanOriginate
Stores implementing `CanOriginate` must override the methods:
 - `OriginateValueAsync`

## CanAnnotate
Stores implementing `CanAnnotate` must override the methods:
 - `MarkRotationCompleteAsync`

## CanRevoke
Stores implementing `CanRevoke` must override the methods:
 - `GetRevocationActionAsync`

# Roles

The stores will fill 3 roles in the rotation process: `Origination`, `Primary storage` and `SecondaryStorage`.  A stores implementation may support multiple roles depending on how the store is used in a rotation plan.

For example, a Key Vault Certificate could be used as the origin for a new secret, could be used as the primary store for rotation metadata for a certificate created externally, or could be used as a secondary store in a plan that propagates certificates into secondary vaults.  However, the RandomString store is useful as a secret origin, but it's incapable of storing rotation metadata or secret values.

## Origination

Stores that support secret origination must implement the `CanOriginate` capability.

## Primary Storage

Primary stores hold rotation metadata like rotation and expiration date, and would typically also store the secret value.

Primary stores must implement the `CanRead` capability. If they're also acting as a secret's origin, e.g. a Key Vault certificate, should implement the `CanAnnotate` capability. If the store is not also acting as origin, it should implement the `CanWrite` capability.

## Secondary Storage

Stores that persist secret values must implement `CanWrite`.  If the store should participate in expiration detection, it should also implement `CanRead`.

Services that only need notification during or after a rotation should be implemented as secondary stores with their notification logic in the `WriteSecretAsync` method.

