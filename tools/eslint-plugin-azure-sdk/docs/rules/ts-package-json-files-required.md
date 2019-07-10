# ts-package-json-files-required

Requires `files` in `package.json` to contain paths to the package contents.

Specifically, this rule looks for inclusion of `dist`, `dist-esm/src`, and `src` as either just those directories or specific subdirectories

## Examples

### Good

```json
{
    "files": [
        "dist",
        "dist-esm/src"
        "src"
    ]
}
```

```json
{
    "files": [
        "./dist",
        "./dist-esm/src"
        "./src"
    ]
}
```

```json
{
    "files": [
        "dist/",
        "dist-esm/src/"
        "src/"
    ]
}
```

```json
{
    "files": [
        "dist/lib",
        "dist-esm/src/lib"
        "src/lib"
    ]
}
```

### Bad

```json
{
  "files": ["dist", "dist-esm/src"]
}
```

```json
{
  "files": ["dist"]
}
```

```json
{
  "files": []
}
```

```json
{}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-package-json-files-required)

Also encompasses [ts-include-cjs](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-include-cjs), [ts-include-esm](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-include-esm), and [ts-include-original-source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-include-original-source)
, as the rules are similar enough to not exist separately for linting purposes.
