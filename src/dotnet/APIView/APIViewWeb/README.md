# APIView

APIView tool is used by archboard reviewers to review API signatures of all public APIs available in Azure SDK packages. This tool generates public API surface level revisions which shows all publicly available classes, methods, properties etc. This makes it easier to identify if there are any breaking changes.

## How does it retrieve public API information

Developers can upload package or an abstract file generated based on each language into APIView tool. APIView tool has a language processor for each language it supports and these individual language processor extracts API stub information and create json APIView file that is processed by tool. APIView tool stores original uploaded file and generated json file in its data store. Revision pages are rendered using this generated json file which contains tokens to present language keywords, links etc. Tool also allows user to recreate review json from stored original file if language package processor itself is updated.

## How to create an API revision manually

API revisions can be created by uploading an artifact to APIView tool. Type of the artifact is different across each language. Some language, for e.g., Swift requires developer to run parser tool locally to generate stub file and upload json stub file instead of any artifact. Following are the detailed instructions on how to create revisions for each language.

### C#
Run `dotnet pack` for the required package to generate Nuget file. Upload the resulting .nupkg file using `Create Review` link in APIView.

### C
1. Install clang 10 or later.
2. Run clang [inputs like az_*.h] -Xclang -ast-dump=json -I ..\..\..\core\core\inc -I "c:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\VC\Tools\MSVC\14.26.28801\include\" > az_core.ast
3. Archive the file Compress-Archive az_core.ast -DestinationPath az_core.zip
4. Upload the resulting archive using `Create Review` link in APIView.

### C++
1. Install clang 10 or later. Your environment may require additional include directories.
2. Run clang++ [input like .\sdk\storage\azure-storage-files-datalake\inc\azure\storage\files\datalake\datalake.hpp] -I .\sdk\storage\azure-storage-files-datalake\inc -I .\sdk\core\azure-core\inc -Xclang -ast-dump -I .\sdk\storage\azure-storage-common\inc -I .\sdk\storage\azure-storage-blobs\inc > Azure_Storage_Files_Datalake.ast
3. Archive the file Compress-Archive Azure_Storage_Files_Datalake.ast -DestinationPath Azure_Storage_Files_Datalake.zip
4. Rename the file Rename-Item Azure_Storage_Files_Datalake.zip -NewName Azure_Storage_Files_Datalake.cppast
5. Upload the resulting archive using `Create Review` link in APIView.

### Go
1. Archive source module directory in which go.mod is present. Compress-Archive ./sdk/azcore -DestinationPath azcore.zip
2. Rename the file Rename-Item azcore.zip -NewName azcore.gosource
3. Upload the resulting archive.

### Java
1. Run `mvn package` build on your project, which will generate a number of build artifacts in the `/target` directory. In there, find the file ending `sources.jar`, and select it.
2. Upload the selected file to APIView tool using `Create Review` link.

### JS/TS
1. Use api-extractor to generate a [docModel](https://api-extractor.com/pages/setup/generating_docs/) file
2. Upload generated api.json file using `Create Review` link.

### Python
1. Generate wheel for the package. python setup.py bdist_wheel -d [dest_folder]
2. Upload generated whl file

### Swagger
Swagger API revisions can be generated manually by uploading swagger file to APIView if you are trying to generate API revision for a single swagger file. Swagger API revision is automatically generated when swagger files are modified in a pull request and pull request comment shows a link to generated APIView. Automatically generated API revision from pull request creates a diff using existing swagger files in the target branch as baseline to show API level changes in pull request.

You can rename a swagger file as mentioned below and upload it to APIView if you need to generate an API revision manually from swagger.
1. Rename swagger json to replace file extension to .swagger `Rename-Item PetSwagger.json -NewName PetSwagger.swagger`
2. Upload renamed `.swagger` file

### TypeSpec
TypeSpec API revision is generated automatically from a pull request and this should be good enough in most scenarios. You can also generate API revision manually for a TypeSpec package by providing URL path to TypeSpec package specification root path.
1. Click and `Create Review` and select TypeSpec from language dropdown.
2. Provide URL to typespec project root path.
