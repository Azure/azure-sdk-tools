## java-sample-clients
1. `mvn compile`
2. `mvn dependency:build-classpath -Dmdep.outputFile=classpath.txt`
3. `java -cp target/classes:$(cat classpath.txt) httpfaultinjectorclient.App`
