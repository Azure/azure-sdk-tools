# ARM Common Types Reference

Standard ARM models from common-types and their Swagger schemas.

## What fields does TrackedResource include in Swagger

`TrackedResource` extends `Resource` and adds `location` (required) and `tags` (optional dictionary). All ARM resources get `id`, `name`, `type` (readOnly) and `systemData` (readOnly) from the base `Resource`.

## What the ManagedServiceIdentity schema looks like in Swagger

TypeSpec: `...ManagedServiceIdentityProperty`

Swagger `identity` object: `type` (enum: None, SystemAssigned, UserAssigned, SystemAssigned,UserAssigned), `principalId` (uuid, readOnly), `tenantId` (uuid, readOnly), `userAssignedIdentities` (dictionary of `UserAssignedIdentity` with `principalId` and `clientId`, both readOnly).

## What the SystemAssignedServiceIdentity schema looks like

TypeSpec: `...ManagedSystemAssignedIdentityProperty`

Same as ManagedServiceIdentity but `type` enum only has `None` and `SystemAssigned`, no userAssignedIdentities.

## What the Sku schema looks like in Swagger

TypeSpec: `...ResourceSkuProperty`

Swagger: `name` (string, required), `tier` (enum: Free, Basic, Standard, Premium), `size` (string), `family` (string), `capacity` (int32).

## What the Plan schema looks like in Swagger

TypeSpec: `...ResourcePlanProperty`

Swagger: `name` (required), `publisher` (required), `product` (required), `promotionCode` (optional), `version` (optional).

## What the ExtendedLocation schema looks like in Swagger

TypeSpec: `...ExtendedLocationProperty`

Swagger: `name` (string), `type` (enum: EdgeZone, CustomLocation).

## What the SystemData schema looks like in Swagger

Automatically added to all ARM resources. Contains `createdBy`, `createdByType` (User/Application/ManagedIdentity/Key), `createdAt` (date-time), `lastModifiedBy`, `lastModifiedByType`, `lastModifiedAt`. All readOnly.

## What the ErrorResponse schema looks like in Swagger

All ARM operations include a `default` response with `ErrorResponse`. Contains `error` → `ErrorDetail` with `code`, `message`, `target`, `details[]` (recursive), `additionalInfo[]`.

## What the CheckNameAvailability request and response look like

Request: `name` (string), `type` (string).
Response: `nameAvailable` (boolean), `reason` (enum: Invalid, AlreadyExists), `message` (string).

## What standard parameter $ref paths are used in Swagger

| Parameter | Common-types $ref path |
|-----------|----------------------|
| api-version | `common-types/resource-management/v5/types.json#/parameters/ApiVersionParameter` |
| subscriptionId | `common-types/resource-management/v5/types.json#/parameters/SubscriptionIdParameter` |
| resourceGroupName | `common-types/resource-management/v5/types.json#/parameters/ResourceGroupNameParameter` |
| location | `common-types/resource-management/v5/types.json#/parameters/LocationParameter` |
| privateEndpointConnectionName | `common-types/resource-management/v5/privatelinks.json#/parameters/PrivateEndpointConnectionName` |

## What Encryption and CustomerManagedKeyEncryption schemas look like

`Encryption`: `keyVaultProperties` ($ref), `status` (enum: enabled, disabled).
`CustomerManagedKeyEncryption`: `keyEncryptionKeyIdentity` ($ref), `keyVaultProperties` ($ref).
