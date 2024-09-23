# Key Vault Secret

## Implementing Class
[KeyVaultSecretStore](../../Azure.Sdk.Tools.SecretRotation.Stores.KeyVault/KeyVaultSecretStore.cs)

## Configuration Key
Key Vault Secret

## Supported Functions
Primary, Secondary

## Parameters

| Name             | Type   | Description                                                                                           |
| ---------------- | ------ | ----------------------------------------------------------------------------------------------------- |
| secretUri        | uri    | uri of the secret in the form of `https://{VaultName}.vault.azure.net/secrets/{SecretName}` |
| revocationAction | string | one of ( `disableVersion`, `none` )                                                                   |
| contentType      | string | The content type string that should be recorded on new secret versions                                |
