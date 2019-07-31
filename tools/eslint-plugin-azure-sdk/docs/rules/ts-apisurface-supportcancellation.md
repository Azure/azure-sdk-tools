# ts-apisurface-supportcancellation

Requires all asynchronous public-facing client methods to accept an `AbortSignalLike` option.

## Examples

### Good

```ts
class ServiceClient {
  async createItem(cancelToken: AbortSignalLike);
}
```

```ts
// private methods are ignored
class ServiceClient {
  private createItem(): void {}
}
```

### Bad

```ts
class ServiceClient {
  async createItem();
}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-apisurface-supportcancellation)
