# ts-package-json-keywords

Requires `keywords` in `package.json` to include `"Azure"` and `"cloud"`.

This rule is fixable using the `--fix` option.

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

## [Source](https://azure.github.io/azure-sdk/typescript_implementation.html#ts-package-json-keywords)
