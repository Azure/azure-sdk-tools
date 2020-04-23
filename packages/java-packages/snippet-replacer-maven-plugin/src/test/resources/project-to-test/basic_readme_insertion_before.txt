##### Create a Configuration Client

Once you have the value of the connection string you can create the configuration client:

<!-- src_embed ./src/samples/java/com/azure/data/appconfiguration/ReadmeSamples.java#L46-L48 -->
```Java
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
    SOME UNRELATED CONTENT THAT SHOULDN'T BE PRESENT ANYMORE WHEN WE RUN UPDATE SNIPPETS
```
<!-- end ./src/samples/java/com/azure/data/appconfiguration/ReadmeSamples.java#L46-L48 -->

or