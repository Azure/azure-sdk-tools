# ts-package-json-author

Requires `author` in `package.json` to be set to `"Microsoft Corporation"`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "author": "Microsoft Corporation"
}
```

### Bad

```json
{
  "author": "Microsoft"
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-author)
