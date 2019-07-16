# ts-modules-only-named

Requires all exports from the main entrypoint to the package to be named exports.

By default, the main entrypoint is assumed to be `src/index.ts`. However, if your package's main entrypoint is elsewhere, you'll need to specify so in your `.eslintrc` configuration file as follows (for example, if the entrypoint is `index.ts`):

```json
{
  "settings": {
    "main": "index.ts"
  }
}
```

## Examples

All examples are representative of the main entrypoint.

### Good

```ts
export /* package contents */{};
```

```ts
export /* package contents */{};
```

```ts
export const package = {
  /* package contents */
};
```

### Bad

```ts
export default {
  /* package contents */
};
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-modules-only-named)

Also encompasses [ts-config-no-default](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-modules-no-default), as the rules are similar enough to not exist separately for linting purposes.
