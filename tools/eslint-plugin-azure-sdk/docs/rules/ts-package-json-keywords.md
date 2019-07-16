# ts-package-json-keywords

Requires `keywords` in `package.json` to include `"Azure"` and `"cloud"`.

## Examples

### Good

```json
{
  "keywords": ["Azure", "cloud"]
}
```

```json
{
  "keywords": ["Azure", "cloud", "sdk"]
}
```

### Bad

```json
{
  "keywords": ["Azure"]
}
```

```json
{
  "keywords": []
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-keywords)
