# ts-package-json-required-scripts

Requires `scripts` in `package.json` to be contain `"build"` and `"test"`.

## Examples

### Good

```json
{
  "scripts": {
    "build": "...",
    "test": "..."
  }
}
```

```json
{
  "scripts": {
    "build": "...",
    "lint": "...",
    "test": "..."
  }
}
```

### Bad

```json
{
  "scripts": {
    "build": "..."
  }
}
```

```json
{
  "scripts": {}
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-required-scripts)
