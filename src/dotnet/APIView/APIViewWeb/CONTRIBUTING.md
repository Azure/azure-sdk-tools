# Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Where to begin

Core of the APIView tool is the web app developed using ASP.Net and TypeScript. This core module takes care of presenting reviews to users, storing review files and metadata in Azure Storage Account and Cosmos database and process requests from Azure Devops pipelines and respond. We also have language level parsers that converts each language specific artifact into a common json stub file that's known to core APIView web app. So, first step as a contributor is to understand the feature or bug fix you would like to submit and identify the area you would like to contribute to. Language parsers are either added as plugin modules developed in .Net or developed using corresponding language as command line tool to extract and generate stub file. If change is specific to a language in how langauge specific API details are extracted to stub tokens then change will be at parser level for that language. If change is applicable for all languages then change will most likely be in core APIView web app.


| Module                        | Source Path                                                                                                                     |
|-------------------------------|---------------------------------------------------------------------------------------------------------------------------------|
| apiview.dev                   | https://github.com/Azure/azure-sdk-tools/tree/main/src/dotnet/APIView/APIViewWeb                                                |
| C#                            | https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIView/Languages/CodeFileBuilder.cs                      |
| C                             | https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/Languages/CLanguageService.cs                  |
| C++                           | https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/Languages/CppLanguageService.cs                |
| Java                          | https://github.com/Azure/azure-sdk-tools/tree/main/src/java/apiview-java-processor                                              |
| JS/TS                         | https://github.com/Azure/azure-sdk-tools/tree/main/tools/apiview/parsers/js-api-parser                                                             |
| Python                        | https://github.com/Azure/azure-sdk-tools/tree/main/packages/python-packages/api-stub-generator                                  |
| Go                            | https://github.com/Azure/azure-sdk-tools/tree/main/src/go                                                                       |
| Swift                         | https://github.com/Azure/azure-sdk-tools/tree/main/src/swift                                                                    |



## Pre-requisites

### Development machine setup

Following are tools required to develop and run test instance of APIView to verify changes locally on your machine.

- Git
- Visual Studio
- .Net
- Any LTS version of Node.js [download](https://nodejs.org/en/download/)
- Java (Optional: Only required if you want to generate Java review locally)
- Python 3.9+ (Optional: Only required if you want to generate Python review locally)
- Go compiler (Optional: Only to generate and test Go reviews)
- Xcode 10.2 or higher (Optional: Only to generate and test Go reviews)
 - Azure subscription with permission to create storage account and Cosmos DB instance.

In addition to local machine setup, you will also require an Azure storage account to store source and stub file and Azure Cosmos database instance to store review metadata. We have added a section below with more details on Azure resources required for testing.


### Azure resources required to run APIView instance locally

You can verify your code changes locally by running a debugging instance of APIView tool locally using visual studio. This still requires Azure storage blob and Azure Cosmos DB instance to store stub file and metadata. Local development and testing do not require any Azure web app instance.

Create following Azure resources in your Azure subscription.

#### Azure storage account

 - Create a storage account in Azure. [Azure storage account](https://docs.microsoft.com/en-us/azure/storage/common/storage-account-create?tabs=azure-portal)
 - Create three blob storage containers with names as follows within the storage account created in previous step: `originals`, `codefiles`, and `usagesamples`

#### Azure Cosmos DB
 - Create a Cosmos DB account in Azure and then create a database with name `APIView` in this account. Once database is created successfully then create three containers in `APIView` database. Following are the list of containers and partition key for each of them. Partition key is case sensitive.

   | Container Name      | Partition Key      |
   |---------------------|--------------------|
   | Reviews             | /id                |
   | Comments            | /ReviewId          |
   | PullRequests        | /PullRequestNumber |
   | UsageSamples        | /ReviewId          |
   


## Getting Started

### Create a GitHub Oath application for local authentication
- Go to `github.com/<your GitHub username>`
- Go to `Settings` -> `Developer Settings`
- Select `OAuth Apps` from menu options
- Click on `New OAuth App`
- Give an application name, for e.g. APIViewDebug and URL `http://localhost:5000`. Add same URL as call back URL also.
- Copy Client ID and Secret for this OAuth app. These are required to be added in configuration to run local debugging instance.


### Clone source code repo 
- Create a new fork of GitHub repo [azure-sdk-tools](https://github.com/Azure/azure-sdk-tools)
- Clone forked repo to development machine.
- Create a new branch from `main` branch of cloned source repo.


### Setup debugging instance using Visual Studio
- If you are making any changes to apiview core web app or any of the languages that are integrated within core web app (C, C++, C#) or to test APIVIew locally then open `Visual Studio` and load `<azure-sdk-tools repo root/src/dotnet/APIView/APIView.sln`


### Connect local debugging instance to Azure resource
Following configuration is required to connect local debug instance to Azure resources as well as to setup debugging environment. Below are the steps to follow and required configuration to be added.

- Right click on `APIViewWeb` project in `APIView solution` using solution explorer in Visual Studio and select `Manage User Secrets`.

- Copy the following to `secrets.json` window opened using manage user secrets and update the configuration with valid GitHub OAuth client ID and secret and Cosmos and Storge connection string.

  {
    "Github": {
        "ClientId": "<Client-ID>",
        "ClientSecret": "<Client OAuthSecret>"
    },
    "Blob": {
        "ConnectionString": "<connection string to storage account>"
    },
    "Cosmos": {
        "ConnectionString": "<connection string to cosmos db>"
    },
    "github-access-token": "",
    "ApiKey": "",
    "PYTHONEXECUTABLEPATH": "<Full path to python executable>",
    "BackgroundTaskDisabled": true
  }

### Compile TypeScript code

APIView web app has some type script code and this needs to be compiled for client side processing. Following are the steps to compile typescript code before starting to debug APIView.

- Go to `<APIVIewWeb directory>/Client`
- run `npm install`
- run `npm run-script build`

This should compile and get client-side scripting as well as CSS ready.

 
### Verify setup

Okay. I have followed all the steps and now I need to verify if it's running fine locally to debug APIView. Download a NuGet file from NuGet package manager for any Azure package to test uploading to API review. One of the options is Azure.Template package [here](https://www.nuget.org/packages/Azure.Template/)

- Select `APIViewWeb` from solution explorer in Visual Studio and run debug or right click and select `Debug` -> `Start New Instance`

- This should open a browser instance with login page to APIView. Click on the Login and this should ask your GitHub credentials and a confirmation to allow OAuth application created earlier to access your GitHub public information.

- If GitHub Client ID configuration in manage user secrets is correct, login should be successful if you are part of Azure or Microsoft GitHub org and org information is publicly available under your GitHub account.

- Home page should be displayed with an empty list of reviews. 

- Click `Create review` button and upload previously downloaded `Azure.Template` package. It should show API review for Azure.Template if everything is setup correctly.


If any of the above steps is showing errors, following are the items to check:

- Verify OAuth Client ID and Secret

- Verify blob connection string and Cosmos DB connection string

- Verify and ensure storage account has two containers as mentioned above

- Verify and ensure cosmos DB instance has all 3 containers as per the instructions above and verify partition key for each of them.


Happy coding!!!!



## I have made my changes. what's next?

 If change is in core apiview web app module then start a debug run using Visual Studio and verify the change using an existing review as well as by creating a new review.

 If change is specific to a language parser, then follow language specific parser testing guidelines. 

 #### C, C++, C#
 - Start Visual Studio debug run and create a new review as per the instructions in [APIView README.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/README.md)
 
 #### Python
 - Install python api stub generator package using Python version configured in `PYTHONEXECUTABLEPATH`
 - Start Visual Studio debug run and create a new review as per the instructions in [APIView README.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/README.md)

 #### Java
 - Build Java package parser jar file and copy the jar file to following location within local code repo <azure-sdk-tools root directory>/artifacts/bin/APIVIewWeb/Debug/<.NetVersion>/
 - Start Visual Studio debug run and create a new review as per the instructions in [APIView README.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/README.md)

 #### Go
 - Compile and build Go source parser into .exe file and copy the .exe file to following location within local code repo <azure-sdk-tools root directory>/artifacts/bin/APIVIewWeb/Debug/<.NetVersion>/
 - Start Visual Studio debug run and create a new review as per the instructions in [APIView README.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/README.md)

 #### JS/TS
  - Got to <azure-sdk-tools repo root>/tools/apiview/parsers/js-api-parser           
  - run `npm install`
  - run `npm run-script build`
  - Copy compiled `export.js` to following location within local code repo <azure-sdk-tools root directory>/artifacts/bin/APIVIewWeb/Debug/<.NetVersion>/
  - Start Visual Studio debug run and create a new review as per the instructions in [APIView README.md](https://github.com/Azure/azure-sdk-tools/blob/main/src/dotnet/APIView/APIViewWeb/README.md)


 Parser version should always be incremented if change is at parser level and if that change is required to be reflected on existing reviews as well. Existing reviews are refreshed to reflect the changes as a backend task if parser version is changed. So, this won't reflect immediately.

 Create a GitHub pull request for the changes to merge it to main branch if code changes are ready and tested thoroughly.
 
