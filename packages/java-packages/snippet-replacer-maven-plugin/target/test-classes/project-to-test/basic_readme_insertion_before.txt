##### Create a Configuration Client

Once you have the value of the connection string you can create the configuration client:

<!-- embedme ./src/samples/java/com/azure/data/appconfiguration/ReadmeSamples.java#L46-L48 -->
```Java
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
    RANDOM CONTENT THAT SHOULDN'T EXIST AFTER WE GENERATE.
```
<!-- end ./src/samples/java/com/azure/data/appconfiguration/ReadmeSamples.java#L46-L48 -->

or
