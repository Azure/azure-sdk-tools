# ts-package-json-repo

Requires `repository` in `package.json` to be set to `"github:Azure/azure-sdk-for-js"`.

## Examples

### Good

```json
{
  "repository": "github:Azure/azure-sdk-for-js"
}
```

### Bad

```json
{
  "repository": "github:Azure/azure-sdk-for-java"
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-repo)
