# Secret Rotation

Rotation happens in 5 main phases:

## Discovery

The rotation plan's primary store is read to get the current value and metadata for a secret.
The expiration date is compared to the plan's expiration and rotation thresholds.
If the `--expired` option is specified and the plan's expiration date is not within the rotation or expiration thresholds, the plan will not be rotated.

## Origination

A new secret value is created by the plan's origin store.  If the origin store doesn't provide an expiration date along with the new value, a default expiration date of `now() + expirationThreshold` is used.  Along with the secret value and expiration date, the origin can include addition tags that may be useful in identifying and revoking a secret value.

## Propagation

The new secret value and expiration date are provided to the secondary stores.  After propagation to secondary stores, the value is propagated to the Primary store indicating that rotation is complete.

## Annotation

Some secret stores can be both Origin and Primary in the same plan.  For example, Key Vault Certificates can originate a new certificate and can store the certificate value with accompanying metadata.

When a store serves as both Origin and Primary, the store must also support Annotation.  Specifically, we expect that the new secret value can be marked as "Rotation Complete" after the Propagation phase, indicating that the rotation process completed successfully.

## Revocation

After rotating to a new secret value, the old value may need to be revoked.  For example, revoking personal access tokens or deleting AAD app secrets.  After rotation, the primary store will be queried for revokable rotation states.  This will include any tags that were added during origination or propagation.  The origin, primary and secondary stores are given the opportunity to perform appropriate revocation actions for each state.
