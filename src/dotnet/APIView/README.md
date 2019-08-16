# APIView

[APIView](https://apiview.azurewebsites.net) is an ASP.NET Core web tool for reviewing APIs.

#### Table of Contents
- What is this for?
- How does it work?
  - Code analyzer
  - Web application
- Areas for future development

## What is this for?

APIView is meant to streamline the API design review process by concisely displaying public contents and supporting collaborative review. Rather than showing all contents of uploaded APIs, only public and protected members will be displayed - additionally, method implementations are hidden. The intent of this is to focus design review on the API features users will interact with while making review of structure and naming conventions as streamlined as possible.

## How does it work?

### Code analyzer

### Web application

APIView is hosted by Azure App Services, under the "APIView" app service in the Azure SDK Developer Playground. For data storage, the project has a storage account - "mcpatdemo" - in the same "t-mcpat" resource group. The app uses Azure blob storage for maintaining its database of APIs and comments, which will be further elaborated on below.

#### Storage scheme

## Areas for future development
