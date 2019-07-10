# ts-package-json-bugs

Requires `bugs` in `package.json` to be set to `"https://github.com/Azure/azure-sdk-for-js/issues"`.

## Examples

### Good

```json
{
  "bugs": {
    "url": "https://github.com/Azure/azure-sdk-for-js/issues"
  }
}
```

### Bad

```json
{
  "bugs": {
    "url": "https://github.com/Azure/azure-sdk-for-java/issues"
  }
}
```

```json
{
  "bugs": {}
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-bugs)
