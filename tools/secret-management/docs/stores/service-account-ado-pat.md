# Service Account ADO PAT

## Implementing Class
[ServiceAccountPersonalAccessTokenStore](../../Azure.Sdk.Tools.SecretRotation.Stores.AzureDevOps/ServiceAccountPersonalAccessTokenStore.cs)

## Configuration Key
Service Account ADO PAT

## Supported Functions
Origin

## Parameters

| Name           | Type | Description                                                                                                |
| -------------- | ---- | ---------------------------------------------------------------------------------------------------------- |
| organization   | string | The name of the Azure DevOps organization. e.g.  For `https://dev.azure.com/azure-sdk`, use `azure-sdk`                                                               |
| patDisplayName   | string | The name to give the new personal access token |
| scopes | string | a comma separated list of scopes to grant the token |
| serviceAccountName | string | the username of the service account |
| serviceAccountPasswordSecret | string | the uri of a Key Vault secret containing the password the the service account |
| serviceAccountTenantId | string | the AAD tenant of the service account |
| revocationAction | string | optional, one of `(revoke, none)` |
