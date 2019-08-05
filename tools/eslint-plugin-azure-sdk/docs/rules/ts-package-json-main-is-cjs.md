# ts-package-json-main-is-cjs

Requires `main` in `package.json` to be point to a CommonJS or UMD module. In this case, its assumed that it points to `"dist/index.js"`.

This rule is fixable using the `--fix` option.

## Examples

### Good

```json
{
  "main": "dist/index.js"
}
```

### Bad

```json
{
  "main": "dist/src/index.js"
}
```

```json
{
  "main": "dist-esm/src/index.js"
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-main-is-cjs)
