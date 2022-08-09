# Codesnippet Maven Plugin

`codesnippet-maven-plugin` allows for Javadocs and READMEs to reference actual Java code. Developers running code 
written by the Azure team want their documentation to be useful. This plugin helps the Azure SDK team deliver on 
that promise by ensuring that code samples presented in doc-comments are always _actual running code_.

## How does it work?

First, reference the plugin in your maven project's `pom.xml` file.

```xml
<plugin>
  <groupId>com.azure.tools</groupId>
  <artifactId>codesnippet-maven-plugin</artifactId>
  <version>1.0.0-beta.8</version>
  <configuration>
    <codesnippetGlob>**/src/samples/java/**/*.java</codesnippetGlob>
    <codesnippetRootDirectory>${project.basedir}/src/samples/java</codesnippetRootDirectory>
    <sourceGlob>**/src/main/java/**/*.java</sourceGlob>
    <sourceRootDirectory>${project.basedir}/src/main/java</sourceRootDirectory>
    <includeSource>true</includeSource>
    <readmePath>${project.basedir}/README.md</readmePath>
    <includeReadme>true</includeReadme>
    <maxLineLength>120</maxLineLength>
    <skip>false</skip>
  </configuration>
  <executions>
    <execution>
      <id>update-codesnippet</id>
      <phase>process-sources</phase>
      <goals>
        <goal>update-codesnippet</goal>
      </goals>
    </execution>
    <execution>
      <id>verify-codesnippet</id>
      <phase>verify</phase>
      <goals>
        <goal>verify-codesnippet</goal>
      </goals>
    </execution>
  </executions>
</plugin>

```

## Execution Goals

`codesnippet-maven-plugin` has two execution goals, `update-codesnippet` and `verify-codesnippet`.

`update-codesnippet` mode runs during your build and _actually updates_ your Javadoc and README codesnippets with 
referenced source code.

`verify-codesnippet` mode is intended to run during CI/PR builds. It will error if Javadoc or README codesnippets don't 
match the reference.

## Defining a Codesnippet Reference

Within working Java code, we have a snippet definition. The string after the `BEGIN:` or `END:` comments is an 
identifier that can be referenced from Javadocs or READMEs.

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

## Injecting Codesnippets into Javadocs

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

## Injecting Codesnippets into READMEs

While the plugin is mostly intended for use in Javadoc comments, it also will run update and verify operations against 
README files.

Example reference before update:

````
```java com.azure.data.applicationconfig.configurationclient.instantiation
```
````

Example of README.md after update:

````
```java com.azure.data.applicationconfig.configurationclient.instantiation
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
```
````

Which Renders:

```java com.azure.data.applicationconfig.configurationclient.instantiation
ConfigurationClient configurationClient = new ConfigurationClientBuilder()
    .connectionString(connectionString)
    .buildClient();
```

