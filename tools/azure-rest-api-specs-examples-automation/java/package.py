import os
from os import path
import platform
import tempfile
import subprocess
import logging
from typing import List

from modules import JavaExample


OS_WINDOWS = platform.system().lower() == 'windows'


def replace_class_name(content: str, old_class_name: str, new_class_name: str) -> str:
    return content.replace('class ' + old_class_name + ' {', 'class ' + new_class_name + ' {', 1)


class MavenPackage:
    tmp_path: str
    package: str
    version: str

    def __init__(self, tmp_path: str, package: str, version: str):
        self.tmp_path = tmp_path
        self.package = package
        self.version = version

    def compile(self, examples: List[JavaExample]) -> bool:
        with tempfile.TemporaryDirectory(dir=self.tmp_path) as tmp_dir_name:
            maven_path = tmp_dir_name

            self.__prepare_workspace(maven_path)

            filename_no = 1
            for example in examples:
                class_name = 'Main' + str(filename_no)
                code_path = path.join(maven_path, 'src', 'main', 'java', class_name + '.java')
                filename_no += 1

                content = replace_class_name(example.content, 'Main', class_name)

                with open(code_path, 'w', encoding='utf-8') as f:
                    f.write(content)

            cmd = ['mvn' + ('.cmd' if OS_WINDOWS else ''), '--no-transfer-progress', 'package']
            logging.info('Run mvn package')
            logging.info('Command line: ' + ' '.join(cmd))
            code = subprocess.run(cmd, cwd=maven_path).returncode
            return code == 0

    def __prepare_workspace(self, maven_path: str):
        # make dir for maven and src/main/java
        java_path = path.join(maven_path, 'src', 'main', 'java')
        os.makedirs(java_path, exist_ok=True)

        # create pom
        pom_file_path = path.join(maven_path, 'pom.xml')
        pom_str = f'''<project xmlns="http://maven.apache.org/POM/4.0.0" xsi:schemaLocation="http://maven.apache.org/POM/4.0.0 http://maven.apache.org/xsd/maven-4.0.0.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <modelVersion>4.0.0</modelVersion>

  <groupId>com.azure.resourcemanager</groupId>
  <artifactId>azure-resourcemanager-example</artifactId>
  <version>1.0.0-beta.1</version>
  <packaging>jar</packaging>

  <name>Example</name>
  <description>Template POM for example.</description>

  <properties>
    <project.build.sourceEncoding>UTF-8</project.build.sourceEncoding>
  </properties>
  <dependencies>
    <dependency>
      <groupId>com.azure.resourcemanager</groupId>
      <artifactId>{self.package}</artifactId>
      <version>{self.version}</version>
    </dependency>
  </dependencies>
  <build>
    <plugins>
      <plugin>
        <groupId>org.apache.maven.plugins</groupId>
        <artifactId>maven-compiler-plugin</artifactId>
        <version>3.8.1</version>
        <configuration>
          <release>8</release>
        </configuration>
      </plugin>
    </plugins>
  </build>
</project>
'''
        with open(pom_file_path, 'w', encoding='utf-8') as f:
            f.write(pom_str)
