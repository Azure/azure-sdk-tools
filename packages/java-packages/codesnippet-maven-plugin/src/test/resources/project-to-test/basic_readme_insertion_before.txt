##### Create a Configuration Client

Once you have the value of the connection string you can create the configuration client:

```java com.azure.data.applicationconfig.configurationclient.instantiation
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
    Some other stuff
```

or

``` java com.azure.core.http.rest.pagedflux.instantiation
```
