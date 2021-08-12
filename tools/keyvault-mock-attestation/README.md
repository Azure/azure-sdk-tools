# Azure Key Vault Mock Attestation service

This folder contains the source code for the Azure Key Vault Mock Attestation service
which is used to run the Secure Key Release live tests for Azure Key Vault.

Secure Key Release requires a signed attestation in order to release the key. In order
to simluate the attestation we created this mock service that can generate a fake key
for testing key release as well as provide endpoints for the Managed HSM to call out
when verifying the attestation token.

## Endpoints

- `GET /generate-test-token`: called by the test itself, it returns a signed token that
  can be passed to the Managed HSM when releasing the key.
- `GET /.well-known/openid-configuration`: the OIDC discovery document containing the
  `jwks_uri` as described in the [OIDC spec](https://openid.net/specs/openid-connect-discovery-1_0.html#ProviderMetadata).
  The service uses `/keys` as the `jwks_uri`.
- `GET /keys`: The `jwks_uri` points to this endpoint, and is used to get the public key of
  the attestation service in order to verify the attestation token.

## How to use the service

This service is published as a Docker container to the Azure SDK Tools Docker container
registry and the image can be used to deploy the service to an Azure App Service or Azure
Container Instance as needed.

> Note: The service is not intended to be used in production, it is only used for testing.

<!-- TODO: link to JS usage when we migrate over to show an example -->

Locally you can run the service by running the following command:

```bash
npm install
npm run start
```

To start an express app service locally using port 5000.
