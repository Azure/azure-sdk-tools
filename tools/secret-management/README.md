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

Additional documentation can be found in the [docs folder](docs/).
