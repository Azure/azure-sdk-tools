# APIView

[APIView](https://apiview.azurewebsites.net) is an ASP.NET Core web tool for reviewing APIs.

#### Table of Contents
- What is this for?
- How does it work?
  - Code analyzer
  - Web application
  - Azure hosting
- Areas for future development

## What is this for?

APIView is meant to streamline the API design review process by supporting collaborative review on concisely presented APIs. Rather than showing all contents of uploaded APIs, only public and protected members will be displayed - additionally, method implementations are hidden. The intent of this is to focus design review on the API features users will interact with while making review of structure and naming conventions as streamlined as possible.

## How does it work?

The app currently supports the review of C# APIs by analyzing uploaded DLLs with Roslyn. A custom model of the API's contents is then displayed for authorized users.

The details of how APIs are represented, presented for review, and handled on the cloud can be found in this section.

### Code analyzer

Code analysis of uploaded files is done using the Microsoft.CodeAnalysis library.

#### Code symbol types

#### Code rendering

#### Testing

### Web application

APIView is an ASP.NET Core project that uses a mix of Razor pages and MVC. User authentication and authorization is done through GitHub, so the application is registered as a GitHub app as well.

#### Configuration

The web application requires a decent bit of configuration to work properly. 

#### GitHub app/authentication

### Azure hosting

APIView is hosted by Azure App Services, under the "APIView" app service in the Azure SDK Developer Playground. For data storage, the project has a storage account - "mcpatdemo" - in the same "t-mcpat" resource group. The app uses Azure blob storage for maintaining its database of APIs and comments, which will be further elaborated on below.

#### Storage scheme

Each API in the app is stored as a single blob, and is associated with a separate blob containing all comments on that API. API blobs are stored in a container called "hello", and comment blobs are in a container called "comments" - by storing APIs and their comments in separate containers, their blob names can be identical without conflict and make fetching blobs for API review pages simpler.

#### App service

## Areas for future development
