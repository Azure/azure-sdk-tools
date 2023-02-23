# Rotation Status

We want to provide a Needs / Doesn't Need rotation report, preferably requiring less access that rotation so it could be run from a service account with lower trust.

Rotation is needed for any secret that's expired or within its rotation window, or where the secondaries have drifted and need to be updated.

For automated expiration and health discovery, we would want to answer:
- Are all discoverable users described in some tracking metadata?
  - This service connection holds a secret that expires. Is that expiration and origin metadata tracked somewhere?
- Are all tracked consumers accessible?
  - I'm looking at tracking metadata that references a service connection.  Does that service connection exist and do I have permission to update it.
- Is tracking metadata complete?
  - Do all secrets have expiration, secret type, and consumer metadata?
  - Is the secret type supported for automated rotation?
  - Does the secret point to documentation for how to manually rotate?

Key Vault can be used as a general metadata store for all expiring secrets, even if no consumers will use the value from key vault

Secrets support up to 15 tags, each of which can have a 256 character name and a 256 character value.  This may not be enough to store all the required metadata to know the issuer, audiences and users of the secret and their rotation parameters.

# Secondary Drift

Like any other synchronization scheme, secret rotation is susceptible to drift. By drift, we mean the tendency for the system to become out of sync, typically through manual changes.

## Drift Detection
Consider a rotation plan that includes a random string and 2 Key Vault secrets.

Immediately after rotation:
```
Primary: some.vault.com/secrets/super-secret
	LastUpdated: 2020-02-21 05:10:02Z
	Expires: 2020-05-21
	Value: abc123

Secondary: another.vault.com/secrets/super-secret
	LastUpdated: 2020-02-21 05:10:00Z
	Expires: 2020-05-21
	Value: abc123
```

### Value based drift detection

When the value or metadata in the secrets no longer matches primary.

Some time later:
```
Primary: some.vault.com/secrets/super-secret
	Expires: 2020-05-21
	Value: abc123

Secondary: another.vault.com/secrets/super-secret
	Expires: 2020-05-21
	Value: xyz789   <-- different
```

Or:

```
Primary: some.vault.com/secrets/super-secret
	Expires: 2022-03-14
	Value: def456

Secondary: another.vault.com/secrets/super-secret
	Expires: 2020-05-21   <-- different
	Value: def456
```


### Timestamp based drift detection

We could also check drift using timestamps from primary and secondary:

```
Primary: some.vault.com/secrets/super-secret
	LastUpdated: 2020-02-21 05:10:02Z

Secondary: another.vault.com/secrets/super-secret
	LastUpdated: 2020-02-23 21:45:00Z
```

We always write to the primary after writing all secondaries. If the secondary reports timestamped state, we can compare the secondary's timestamp to the primary's to see if it was modified after writing the primary. This would indicate drift.

### Undiscoverable Drift (Write-only secondaries)

In the case of write-only secondaries like Azure DevOps service connections, there's no value, metadata or timestamps available to tell you the current state. We must either assume it's still synchronized, or assume it's drifted.

## Drift Correction

For correction of secondary drift, we could either originate a new value, or simply propagate the existing value from primary back into the secondaries. If we do propagate the existing value from the primary to a secondary, we must annotate the primary with the new timestamp to prevent the appearance of drift caused by the secondary timestamp being after the primary timestamp.

# Zero-downtime rotation

Secret rotation has 4 stages:
- Origination: the creation of a new secret value
- Trust: the new value is considered valid by the audience
- Propagation: the new value is provided to users
- Revocation: the old value is no longer valid at the audience

For zero-downtime rotation, we need all users of a secret to have access to a value that all audiences trust at all times.  This requires ordering Trust and Propagation based on the number of values an Audience simultaneously trust.

Zero-downtime rotation isn't possible when a user has access to only 1 trusted value, an audience can trust only 1 value at a time, and Trust and Propagation aren't atomic.

In scenarios where either the audience trusts multiple values or a user has access to multiple values, zero-downtime rotation is possible.

## Single-Trust / Multi-Use

- An audience can trust only a single value of a secret (e.g. password on a service account)
- Users have access to both the old value and new value
- Users need to conditionally fall back to the old value if the audience doesn't trust the new value

Prior to rotation at the audience, the audience will consider the new value invalid.  This will result in a temporary failure state. Users should detect this and fall back to using the old value.

## Multi-Trust

- An audience can trust multiple values of a secret (e.g. access keys on a storage account)
- Users have access to either a single value or multiple values

When an audience can trust multiple values, trust can be established before propagation.  This allows the users to use the old value while the new value is being propagated.

## Revocation

Old values should be revoked at the audience once all users have begun using the new value.  Delays may exist between Propagation or Trust and revocation when users hold a value in a long running process before use, e.g. pipeline jobs where a secret value is held in an environment variable for long periods of time.
