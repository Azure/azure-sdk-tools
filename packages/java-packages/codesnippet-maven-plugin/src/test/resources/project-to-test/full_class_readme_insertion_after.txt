##### Configuration Client Examples

The following is a bunch of examples:

```java full-class-readme-sample
/**
 * This class contains code samples for generating javadocs through doclets for {@link ConfigurationClient}
 */
public final class ConfigurationClientJavaDocCodeSnippets {

    private String key1 = "key1";
    private String key2 = "key2";
    private String value1 = "val1";
    private String value2 = "val2";

    /**
     * Generates code sample for creating a {@link ConfigurationClient}
     *
     * @return An instance of {@link ConfigurationClient}
     * @throws IllegalStateException If configuration credentials cannot be created.
     */
    public ConfigurationClient createAsyncConfigurationClientWithPipeline() {

    /**
     * Generates code sample for creating a {@link ConfigurationClient}
     *
     * @return An instance of {@link ConfigurationClient}
     * @throws IllegalStateException If configuration credentials cannot be created
     */
    public ConfigurationClient createSyncConfigurationClient() {
        String connectionString = getConnectionString();
        ConfigurationClient configurationClient = new ConfigurationClientBuilder()
            .connectionString(connectionString)
            .buildClient();
        return configurationClient;
    }

    /**
     * Generates code sample for using {@link ConfigurationClient#addConfigurationSetting(String, String, String)}
     */
    public void addConfigurationSetting() {
        ConfigurationClient configurationClient = createSyncConfigurationClient();
        ConfigurationSetting result = configurationClient
            .addConfigurationSetting("prodDBConnection", "westUS", "db_connection");
        System.out.printf("Key: %s, Label: %s, Value: %s", result.getKey(), result.getLabel(), result.getValue());
    }

    public void encodedHtmlWorks(){
        // A supplier that fetches the first page of data from source/service
        Supplier<Mono<PagedResponse<Integer>>> firstPageRetriever = () -> getFirstPage();

        // A function that fetches subsequent pages of data from source/service given a continuation token
        Function<String, Mono<PagedResponse<Integer>>> nextPageRetriever =
            continuationToken -> getNextPage(continuationToken);

        PagedFlux<Integer> pagedFlux = new PagedFlux<>(firstPageRetriever, nextPageRetriever);
    }
}
```
