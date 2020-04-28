##### Create a Configuration Client

Once you have the value of the connection string you can create the configuration client:

```Java com.azure.data.applicationconfig.configurationclient.instantiation
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
    Some other crap
```

or

``` Java com.azure.core.http.rest.pagedflux.instantiation
```
