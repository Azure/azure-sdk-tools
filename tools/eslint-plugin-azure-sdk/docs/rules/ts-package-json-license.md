# ts-package-json-license

Requires `license` in `package.json` to be set to `"MIT"`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "license": "MIT"
}
```

### Bad

```json
{
  "license": "ISC"
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-license)
