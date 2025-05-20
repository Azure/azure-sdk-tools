# ReportHelper Project

## Overview
The `ReportHelper` project is used for processing and comparing data, as well as generating reports in various formats.

## Code Components

- **appsettings.json**  
  A configuration file used to specify which package's test data will be compared during the execution of the Program.cs. For example:
  ```json
    {
      "PackageName": "azure-storage-blob"
    }
  ```
- **Program.cs**  
  Acts as the entry point of the project. It reads the current and previous test data, invokes comparison methods, and saves the results in JSON, Excel, and TXT formats.

- **ConstData.cs**  
  Defines file paths for reading and storing data.

- **Models.cs**  
  Defines the data structure for test results. It facilitates saving program data as JSON files and loading JSON file data into the program.

- **ReportHelper4Test.cs**  
  Provides methods to save test results or comparison data in JSON, Excel, and TXT formats (Markdown format, designed for submitting issues on GitHub).