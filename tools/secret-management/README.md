Additional documentation can be found in the [docs folder](docs/).

# Secret Management

The secret management tool provides configuration driven orchestration of:

- secret origination, propagation, revocation and metadata storage.
- role based access control and federated identity credential management
- syncing of secrets to github actions contexts

If the tool's installed locally, it's invoked like:

```
dotnet tool run secrets --help
```

If the tool's installed globally, it's invoked like:

```
secrets --help
```

# Implemented Stores

| Configuration Key                | Links                                                            |
| -------------------------------- | ---------------------------------------------------------------- |
| AAD Application Secret           | [documentation](docs/stores/aad-application-secret.md)           |
| ADO Service Connection Parameter | [documentation](docs/stores/ado-service-connection-parameter.md) |
| Azure Website                    | [documentation](docs/stores/azure-website.md)                    |
| Key Vault Certificate            | [documentation](docs/stores/keyvault-certificate.md)             |
| Key Vault Secret                 | [documentation](docs/stores/keyvault-secret.md)                  |
| Manual Action                    | [documentation](docs/stores/manual-action.md)                    |
| Random String                    | [documentation](docs/stores/random-string.md)                    |
| Service Account ADO PAT          | [documentation](docs/stores/service-account-ado-pat.md)          |
