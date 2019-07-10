# ts-package-json-name

Requires `name` in `package.json` to be set to `"@azure/<service-name>"`, with the service name in kebab-case.

## Examples

### Good

```json
{
  "name": "@azure/service-bus"
}
```

### Bad

```json
{
  "name": "@microsoft/service-bus"
}
```

```json
{
  "name": "service-bus"
}
```

```json
{
  "name": "@azure/serviceBus"
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-name)
