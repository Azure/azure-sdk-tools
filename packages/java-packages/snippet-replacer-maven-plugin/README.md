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

### Example of `update`



### Example of `verify`

### Functionality against root README.md

While the plugin is mostly intended for use in Javadoc comments, it also will run update and verify operations against the file ${project.basedir}/README.md.

Example reference before update:

````
```Java com.azure.data.applicationconfig.configurationclient.instantiation
```
````

Example of README.md after update:

````
```Java snippet:com.azure.data.applicationconfig.configurationclient.instantiation
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

####
