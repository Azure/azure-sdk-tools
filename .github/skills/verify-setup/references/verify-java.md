# Java SDK Requirements

## Required Checks

| Requirement | Check Command | Min Version | Purpose | Auto Install | Installation Instructions |
|-------------|---------------|-------------|---------|--------------|--------------------------|
| Java | `java -version` | 17.0.0 | Java Development Kit | false | **Linux:** `sudo apt install openjdk-17-jdk`<br>**macOS:** `brew install openjdk@17`<br>**Windows:** Download JDK<br>Then: Set JAVA_HOME, add JAVA_HOME/bin to PATH, restart IDE |
| Maven | `mvn -v` | - | Build automation tool | false | **Linux:** `sudo apt install maven`<br>**macOS:** `brew install maven`<br>**Windows:** Download Maven<br>Then: Set MAVEN_HOME, add MAVEN_HOME/bin to PATH, restart IDE |
