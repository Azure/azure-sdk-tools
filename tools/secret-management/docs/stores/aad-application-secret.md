# AAD Application Secret

## Implementing Class
[AadApplicationSecretStore](../../Azure.Sdk.Tools.SecretRotation.Stores.AzureActiveDirectory/AadApplicationSecretStore.cs)

## Configuration Key
AAD Application Secret

## Supported Functions
Origin

## Parameters

| Name             | Type   | Description                                                              |
| ---------------- | ------ | ------------------------------------------------------------------------ |
| applicationId    | string | The ID of the AAD Application Registration on which to create the secret |
| displayName      | string | The name of the application secret to create                             |
| revocationAction | string | optional, one of ( `delete`, `none` ). defaults to none                  |
