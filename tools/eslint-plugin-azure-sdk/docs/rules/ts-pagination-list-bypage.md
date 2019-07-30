# ts-pagination-list-bypage

Requires the returned object in a client's `list` method to contain a `byPage` function with `continuationToken` and `maxPageSize` tokens.

## Examples

### Good

```ts
class ServiceClient {
  listItems(): PagedAsyncIterableIterator<Item> {
    return {
      byPage(continuationToken, maxPageSize) {}
    };
  }
}
```

### Bad

```ts
// no byPage function
class ServiceClient {
  listItems(): PagedAsyncIterableIterator<Item> {
    return {};
  }
}
```

```ts
// missing parameters
class ServiceClient {
  listItems(): PagedAsyncIterableIterator<Item> {
    return {
      byPage() {}
    };
  }
}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-pagination-list)
