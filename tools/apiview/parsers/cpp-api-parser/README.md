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

- `-r`, `--review` - Specifies the name of the API review for the API Review tool.
- `--packageVersion` - Specifies the version of the package for the API Review.
- `-o`, `--output` - Specifies the output file for the API review.
- `--version` - Prints the version of the ParseAzureSdkCpp tool.
- `-h`, `--help` - Prints help text about the tool.
- `-c`, `--console` - Prints the ApiView output to the console as well as the output JSON file.

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

- "sourceFilesToProcess" - if present and non-null, represents an array of
  source files which describes the
  files which define the APIs included in the API Review. If this is not
  present, the entire source tree under the input directory
  is scanned for headers to include.
- "sourceFilesToSkip" - If sourceFilesToProcess is not present or is null,
  this is a set of files which should be excluded from the
  source tree scan.
- "additionalIncludeDirectories" - if present and non-null, represents an
  array of directories which are added to the include path
  when compiling the source files.
- "reviewName" - The name of the API review, used if the -r command line
  switch is not present (note that when run in the CI pipeline, the -r command line
  switch will almost always be specified).
- "packageName" - The name of the package being reviewed.
- "serviceName" - The name of the service being reviewed (preferably as represented in ARM).
- "additionalCompilerSwitches" - if present and non-null, represents an
  array of additional compiler switches to pass to the
  compiler.
- "allowInternal" - if present and true, types in the "\_internal"
  namespace will not generate an error.
- "includeDetail" - if present and true, includes types in the "\_detail"
  namespace in the API Review.
- "includePrivate" - if present and true, includes private APIs in the API
  Review.
- "filterNamespace" - if present and non-null, represents a set of
  namespace prefixes which are expected in the package.
  Types which do not match the filter will generate a warning.
- "sourceRootUrl" - if present and non-null represents the root URL for the ApiView directory.
  This URL is used to generate source links in the ApiView tool.

## Implementation Details

The ParseAzureSdkCpp tool uses the [LLVM tooling infrastructure](https://clang.llvm.org/docs/LibTooling.html) to create the Azure SDK.

After parsing the command line, the tool (in ParseAzureSdkCpp\ParseAzureSdkCpp.cpp)
instantiates an `ApiViewProcessor` passing in the `<input-directory>` directory to be parsed to the constructor.
The `ApiViewProcessor` object opens the `ApiViewSettings.json` file in that directory and parses it to
read additional compiler settings and options, as well as global configuration options for the API review.

Note that the _actual_ parsing implementation is in the `ProcessorImpl.cpp` and
`ProcessorImpl.hpp` files - this is done to isolate the clang/LLVM implementation
details from the rest of the implementation.

Once the ApiView processor is created, the tool calls `apiViewProcessor.ProcessApiView()` which will invoke the clang tooling to parse
the input files and create an `AzureClassesDatabase` which contains the types and constructs to be included in the API review.

Finally, after collecting the types in the `AzureClassesDatabase`, the ParseAzureSdkCpp tool instantiates an instance of a 'JsonDumper' 
object to dump the package.

### Source Layout

There are three major directories in the C++ API View processor:

- `ApiViewProcessor` - the files in this directory performs the meat of processing the package publics.
- `ParseAzureSdkCpp` - the files in this directory provide the "main" function of the application.
- `ParseTests` - test collateral for the ApiView processor.

### ApiViewProcessor details

Within the ApiViewProcessor directory, there are three major pieces:

- `ApiViewProcessorImpl` - Parses C++ source files and gathers all the relevant types into a `AzureClassesDatabase`.
- `AstNode` - The `AstNode` represents a type which will be processed in an ApiView.
- `AstDumper` - An abstract base type used to dump API View JSON objects.

#### ApiViewProcessorImpl implementation

The ApiViewProcessorImpl object is an implementation of a [`clang::ASTFrontEndAction`](https://clang.llvm.org/doxygen/classclang_1_1ASTFrontendAction.html),[`clang::ASTConsumer`](https://clang.llvm.org/doxygen/classclang_1_1ASTConsumer.html), and [`clang::RecursiveASTVisitor`](https://clang.llvm.org/doxygen/classclang_1_1RecursiveASTVisitor.html). The meat of the work is done in the `CollectCppClassesVisitor` which iterates over every named object within the parsed sources (in the `VisitNamedDecl` method).

The `ApiViewProcessorImpl` class also includes information retrieved from the `ApiViewSettings.json` file which can be used
by the `AstNode` functions to filter and/or modify how the output of the API View is generated.

#### AstNode
The `AstNode` and `AstNamedNode` classes are the workhorses of the API view processor.
Each `AstNode` object represents a construct in the parsed syntax tree which should be dumped.

There are two major functions for each `AstNode`:

- `AstNode::Create` - Instantiates a new `AstNode` object from a `clang::Decl` object.
- `DumpNode` - Dumps an instance of an `AstNode` derived class to an `AstDumper` object.

##### AstType and AstExpr

The `AstType` and `AstExpr` family of classes express a "type" or "expression",
which are used to represent the types of parameters or default values for those
parameters.

#### AstDumper

The `AstDumper` class defines an abstract base class which can be extended to enable
different output scenarios. Within the `AstDumper` class, there are a number of
conventions associated with the methods contained within.

Methods named `Insert<xxx>`, `Add<xxx>`, and `Dump<xxx>` represent pure virtual methods which
must be implemented by a specialization of the `AstDumper` to express the construct
in a fashion appropriate to the specialization.

All other methods are implemented in the AstDumper class and use the `Insert<xxx>`
and `Add<xxx>` methods. This allows the AST dumping to maintain a set of high
level constructs such as soft line breaks (to handle intelligent line wrapping),
Newline processing, indentation processing, etc.

There are two AstDumper objects in the `ApiViewProcessor` directory - `TextDumper` and `JsonDumper`. 
The `TextDumper` object will dump the output of the ApiView as text to a `std::ostream` object, 
while the `JsonDumper` object will dump the output of the ApiView as JSON to a `std::ostream` object (the `TextDumper`
object is primarily used for test purposes).

##### ApiView tool inputs

When the `JsonDumper` class emits an API View JSON file, it emits a tokenized representation of the source file to be
displayed in the ApiView tool.

This tokenized representation is modeled in the following example JSON document found [here](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/apiview_token_gist.json)
When this JSON file is parsed by the API View tool, it will create the following text in the ApiView:
![API View snippet defining `ClassLibrary1.dll`](https://i.imgur.com/ikfRmLM.png)

#### VCPKG notes

The `ParseAzureSdkCpp` tool uses a custom port for clang-15 because vcpkg does not currently have a port for clang-15 in the public repository.

The clang-15 port files were taken from the [vcpkg repository](https://github.com/microsoft/vcpkg/pull/26902).
