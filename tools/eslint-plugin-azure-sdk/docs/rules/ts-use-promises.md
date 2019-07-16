# ts-use-interface-parameters

Requires the usage of in-built Promises instead of those from libraries or polyfills.

## Examples

### Good

```ts
const promise = (): Promise<string> => {
  return new Promise(resolve => resolve("hi"));
};
```

### Bad

```ts
import Promise from "bluebird"; // or any such library

const promise = (): Promise<string> => {
  return new Promise(resolve => resolve("example"));
};
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-use-promises)
