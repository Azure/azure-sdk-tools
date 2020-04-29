# Snippet Replacer

`snippet-replacer-maven-plugin` allows java devs to reference actual java code in their javadoc comments. Developers running code written by the Azure team want their documentation to be useful. This plugin helps the Azure SDK team deliver on that promise by ensuring that code samples presented in doc-comments is always _actual running code_.

### How does it work?

First, reference the plugin in your maven project's `pom.xml` file.

```xml
<plugin>
  <groupId>com.azure.tools</groupId>
  <artifactId>snippet-replacer-maven-plugin</artifactId>
  <version>1.0.0</version>
  <configuration>
    <mode>update</mode>
    <targetDir>${project.basedir}</targetDir>
  </configuration>
  <executions>
    <execution>
      <phase>process-sources</phase>
      <goals>
        <goal>snippet-engine</goal>
      </goals>
    </execution>
  </executions>
</plugin>

```

The plugin is intended to be run in the `process-sources` phase. 

### The modes of operation

`snippet-replacer` has two modes, `update` and `verify`.

`update` mode runs during your build and _actually updates_ your Javadoc comments with referenced source code.
`verify` mode is intended to be run during CI/PR builds. It will error a  javadoc comment that is not updated.

## How to define a referencable snippet

Embed a beginning and end html comment wherever you want the source code inserted.

Within working java code, we have a snippet definition. The string after the `BEGIN:` or `END:` comments is an identifer that can be referenced from javadoc or readmes.

```
public ConfigurationClient createSyncConfigurationClient() {
    String connectionString = getConnectionString();
    // BEGIN: com.azure.data.applicationconfig.configurationclient.instantiation
    ConfigurationClient configurationClient = new ConfigurationClientBuilder()
        .connectionString(connectionString)
        .buildClient();
    // END: com.azure.data.applicationconfig.configurationclient.instantiation
    return configurationClient;
}
```
The above example defines a code snippet of identifier `com.azure.data.applicationconfig.configurationclient.instantiation`.

### Example of `update`

Within a javadoc comment, a snippet is referenced by a matching pair html comments. 

```
...
 * <p><strong>Instantiating a synchronous Configuration Client</strong></p>
 * 
 * <!-- src_embed com.azure.core.http.rest.pagedflux.instantiation -->
 * <!-- end com.azure.core.http.rest.pagedflux.instantiation -->
 *
 * <p>View {@link ConfigurationClientBuilder this} for additional ways to construct the client.</p>
 *
 * @see ConfigurationClientBuilder
 */
@ServiceClient(builder = ConfigurationClientBuilder.class, serviceInterfaces = ConfigurationService.class)
public final class ConfigurationClient {
...
```
After update runs:
```
...
 * <p><strong>Instantiating a synchronous Configuration Client</strong></p>
 * 
 * <!-- src_embed com.azure.core.http.rest.pagedflux.instantiation -->
 * <pre>
 * ConfigurationClient configurationClient = new ConfigurationClientBuilder&#40;&#41;
 *     .connectionString&#40;connectionString&#41;
 *     .buildClient&#40;&#41;;
 * </pre>
 * <!-- end com.azure.core.http.rest.pagedflux.instantiation -->
 *
 * <p>View {@link ConfigurationClientBuilder this} for additional ways to construct the client.</p>
 *
 * @see ConfigurationClientBuilder
 */
@ServiceClient(builder = ConfigurationClientBuilder.class, serviceInterfaces = ConfigurationService.class)
public final class ConfigurationClient {
...
```

The referenced code snippet will be embedded (with javadoc appropriate encoding) with the properly spaced code snippet.

### Example of `verify`

This mode is intended for use within CI systems. It will throw an error detailing where snippets are in need of updating. Any dev can rebuild your project with `update` mode and commit the result.

### Functionality against root README.md

While the plugin is mostly intended for use in Javadoc comments, it also will run update and verify operations against the file ${project.basedir}/README.md.

Example reference before update:

````
```Java com.azure.data.applicationconfig.configurationclient.instantiation
```
````

Example of README.md after update:

````
```Java com.azure.data.applicationconfig.configurationclient.instantiation
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
```
````

Which Renders:

```Java com.azure.data.applicationconfig.configurationclient.instantiation
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
```

