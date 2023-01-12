# C++ Api Review Parser

The C++ Api Review Parser parses the header files of a C++ project and generates an output file suitable to use with the [Api View](https://ApiView.dev)
site.

## Using the ParseAzureSdkCpp tool

The simplest invocation of the tool is as follows:

```powershell
C:> ParseAzureSdkCpp -o <output-file> <input-directory>
```

where `<output-file>` is a JSON file which holds the API Review to be uploaded to the ApiView web site, and `<input-directory>` specifies the directory which contains the header files to be parsed.

For instance, if your azure-sdk-for-cpp directory is located at C:\AzureCppSdk, the following will generate an API Review for the Azure Core package:

```powershell
C:> ParseAzureSdkCpp C:\AzureCppSdk\sdk\core\azure-core\inc -o core.json
```

The `ParseAzureSdkCpp` tool has the following command line switches:

* `-r`, `--review` - Specifies the name of the API review for the API Review tool.
* `--packageVersion` - Specifies the version of the package for the API Review.
* `-o`, `--output` - Specifies the output file for the API review.
* `--version` - Prints the version of the ParseAzureSdkCpp tool.
* `-h`, `--help` - Prints help text about the tool.

### ApiViewSettings.json

In order to create an API Review, there is a set of additional configuration information needed to be provided. This information is provided in a JSON file named ApiViewSettings.json in the `<input-directory>` location.

 The following is an example of such a configuration file:

```json
{
  "sourceFilesToProcess": null,
  "additionalIncludeDirectories": ["../../../core/azure-core/inc"],
  "sourceFilesToSkip": [],
  "additionalCompilerSwitches": [],
  "allowInternal": true,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Azure::Storage",
  "reviewName": "Azure Storage Common API Review",
  "serviceName": "Azure Storage",
  "packageName": "azure-storage-common-cpp"
}
```

An ApiViewSettings.json file contains the following options:

* "sourceFilesToProcess" - if present and non-null, represents an array of
  source files which describes the
  files which define the APIs included in the API Review. If this is not
  present, the entire source tree under the input directory
  is scanned for headers to include.
* "sourceFilesToSkip" - If sourceFilesToProcess is not present or is null,
  this is a set of files which should be excluded from the
  source tree scan.
* "additionalIncludeDirectories" - if present and non-null, represents an
  array of directories which are added to the include path
  when compiling the source files.
* "reviewName" - The name of the API review, used if the -r command line
  switch is not present (note that when run in the CI pipeline, the -r command line
switch will almost always be specified).
* "packageName" - The name of the package being reviewed.
* "serviceName" - The name of the service being reviewed (preferably as represented in ARM).
* "additionalCompilerSwitches" - if present and non-null, represents an
  array of additional compiler switches to pass to the
  compiler.
* "allowInternal" - if present and true, types in the "\_internal"
  namespace will not generate an error.
* "includeDetail" - if present and true, includes types in the "\_detail"
  namespace in the API Review.
* "includePrivate" - if present and true, includes private APIs in the API
  Review.
* "filterNamespace" - if present and non-null, represents a set of
  namespace prefixes which are expected in the package.
  Types which do not match the filter will generate a warning.

## Implementation Details

The ParseAzureSdkCpp tool uses the LLVM tooling infrastructure to create the Azure SDK.

After parsing the command line, the tool (in ParseAzureSdkCpp\ParseAzureSdkCpp.cpp)
instantiates an `ApiViewProcessor` passing in the `<input-directory>` directory to be parsed to the constructor.
The `ApiViewProcessor` object opens the `ApiViewSettings.json` file in that directory and parses it to
read additional compiler settings and options, as well as global configuration options for the API review.

Note that the *actual* parsing implementation is in the `ProcessorImpl.cpp` and
`ProcessorImpl.hpp` files - this is done to isolate the clang/LLVM implementation
details from the rest of the implementation.

The tool then calls `apiViewProcessor.ProcessApiView()` which will invoke the clang tooling to parse
the input files and create an `AzureClassesDatabase`.
