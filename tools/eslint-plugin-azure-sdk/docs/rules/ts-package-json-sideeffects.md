# ts-package-json-sideeffects

Requires `sideEffects` in `package.json` to be set to `false`.

## Examples

### Good

```json
{
    "sideEffects": false
}
```

### Bad

```json
{
    "sideEffects": true
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-sideeffects)
