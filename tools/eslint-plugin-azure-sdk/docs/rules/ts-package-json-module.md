# ts-package-json-module

Requires `module` in `package.json` to be set to the ES6 entrypoint of the package. It is assumed that this is `"dist-esm/src/index.js"`.

## Examples

### Good

```json
{
  "module": "dist-esm/src/index.js"
}
```

### Bad

```json
{
  "module": "dist-esm/src/lib/index.js"
}
```

```json
{
  "module": "dist/index.js"
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-module)
