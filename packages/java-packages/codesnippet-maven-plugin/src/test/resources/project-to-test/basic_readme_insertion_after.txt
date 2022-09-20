##### Create a Configuration Client

Once you have the value of the connection string you can create the configuration client:

```java com.azure.data.applicationconfig.configurationclient.instantiation
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
```

or

``` java com.azure.core.http.rest.pagedflux.instantiation
// A supplier that fetches the first page of data from source/service
Supplier<Mono<PagedResponse<Integer>>> firstPageRetriever = () -> getFirstPage();

// A function that fetches subsequent pages of data from source/service given a continuation token
Function<String, Mono<PagedResponse<Integer>>> nextPageRetriever =
    continuationToken -> getNextPage(continuationToken);

PagedFlux<Integer> pagedFlux = new PagedFlux<>(firstPageRetriever,
    nextPageRetriever);
```
