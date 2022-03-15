# APIView

APIView tool is used by archboard reviewers to review API signatures of all public APIs available in Azure SDK packages. This tool generates public API surface level review which shows all publicly available classes, methods, properties etc. This makes it easier to identify if there are any breaking changes. Currently APIView tool supports following languages:

- C#
- C
- C++
- Java
- JS/TS
- Python
- Go
- Swift

## Why do we need APIView

APIView tool allows to see only stub version of classes, methods, properties, method signatures available in each Azure SDK. This helps architects to review an Azure SDK and helps to identify any potential change that can impact consumers of an Azure SDK. APIView is also used as an enforcing tool to make sure we release a GA version of Azure SDK package only after it is approved by an architect. APIView is also utlized to identify any change at API level in a pull request.


## How to create an API review manually

API review can be created by uploading an artifact to APIView tool. Type of the artifact is different across each language. Some language, for e.g., Swift requires developer to run parser tool locally to generate stub file and upload json stub file instead of any artifact. Following are the detailed instructions on how to create review for each language. 

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


## How does it retrieve public API information

Developers can upload package or an abstract file generated based on each language into API View tool. APIView tool has a language processor for each language it supports and these individual language processor extracts API stub information and create json APIView file that is processed by tool. API review tool stores original uploaded file and generated json file in its data store. Review pages are rendered using this generated json file which contains tokens to present language keywords, links etc. Tool also allows user to recreate review json from stored original file if language package processor itself is updated.

## Types of API reviews

APIView tool shows three different types of reviews based on how it is generated.
- Manual
- Automatic
- Pull request reviews

### Manual
Manual reviews are created by developers by uploading artifact as per the instructions given above. This will allow developers to review API changes if API review is not available from PR branch.

### Automatic
API review tool has a master version of API review created automatically by azure-sdk bot as part of our scheduled CI pipelines in Azure Devops internal project. CI will check for any new change in public API surface level as part of every scheduled run and create a new revision if it finds any difference. These reviews cannot be deleted or updated with new revisions manually , in other words, this is a locked version of API reviews. Only actions that are allowed on master review is Add/update/remove/Resolve comment and Approve API reviews.

As part of build and release pipelines, we will enforce API approval by architect or deputy architect if package version is GA which means we need to ensure latest revision of automatically created review is approved in API review tool to release a GA version package. This is applicable for any package ( both data plane and management plane) that is listed as an artifact in CI yaml.

Automatic API reviews are not listed by default when you login to API review tool. You can view automatic reviews by clicking on "Automatic" button in top right corner in the main page.

### API reviews from pull requests
PR pipeline for Java, C#, JS and Python sends a request to APIView tool to identify if there are any changes made at API level as part of the PR it is processing. APIView tool compared the stub file from PR pipeline against API review revision created from main branch and creates a new review if there is any change. APIView will also add a comment to GitHub PR with a link to API review if there is any change detected. APIView will not create any review for a PR if it does not have any API level change.