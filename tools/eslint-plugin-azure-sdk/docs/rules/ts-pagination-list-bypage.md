# ts-pagination-list-bypage

Requires the returned object in a client's `list` method to contain a `byPage` function with `continuationToken` and `maxPageSize` options.

Specifically, these options should be wrapped in an object matching the `PageSettings` interface from `@azure/core-paging`.

## Examples

### Good

```ts
class ServiceClient {
  listItems(): PagedAsyncIterableIterator<Item> {
    return {
      byPage({ continuationToken, maxPageSize }) {}
    };
  }
}
```

### Bad

```ts
// missing parameter
class ServiceClient {
  listItems(): PagedAsyncIterableIterator<Item> {
    return {
      byPage({ continuationToken }) {}
    };
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

```ts
// not wrapped in an object
class ServiceClient {
  listItems(): PagedAsyncIterableIterator<Item> {
    return {
      byPage(continuationToken, maxPageSize) {}
    };
  }
}
```

## [Source](https://azuresdkspecs.z5.web.core.windows.net/TypeScriptSpec.html#ts-pagination-list)
