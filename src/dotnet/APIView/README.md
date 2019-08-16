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

Code analysis of uploaded files is done using the Microsoft.CodeAnalysis library. An uploaded API's semantic model is traversed, and custom types are generated at each symbol node of the tree to capture attributes pertinent to review. These types are then rendered in a regular code format (with the exclusion of method bodies) when an API is under review.

#### Symbol types

All types to represent code symbols can be found under the SymbolTypes folder of the APIView project. These types each have a number of properties that record information necessary to properly re-render code: accessibility, return type, parameter(s), etc. 

Additionally, there is an `Id` property for `EventApiView`, `FieldApiView`, `NamedTypeApiView`, `NamespaceApiView`, `PropertyApiView`, `TokenApiView`, `MethodApiView`, and `AttributeApiView` types. With the exception of the `MethodApiView` and `AttributeApiView` types, this ID is used to generate HTML anchors for click-based navigation. The others use this ID to link to anchors or commentable tokens: `MethodApiView` types carry the ID of their containing named type if they're constructors for navigation, and `AttributeApiView` types carry the ID of the symbol they're applied to, which will be targeted for comments upon clicking.

#### Rendering

There are also types that are used explicitly for rendering purposes, which can be found under the Rendering folder of the APIView project.

`TreeRendererApiView` is a base type that walks down the tree of assembly types and calls the appropriate rendering method for each symbol. `HtmlRendererApiView` and `TextRendererApiView` inherit from this base type and implement the rendering methods to render types into HTML or plain text strings, respectively.

To allow for greater ease when inserting HTML between lines of code on API review pages, each line of code is rendered as an independent string through the use of the `LineApiView` class. Each line contains a display string as well as a potential `ElementId` property, which would be populated with the ID of the commentable symbol existing on that line (each line contains a maximum of one commentable symbol). The `StringListApiView` class inherits from `List<LineApiView>` to allow for simplified rendering of multiple code lines.

#### Testing

In the APIViewTest project are a number of unit tests, divided into symbol type. These tests generally test three aspects of the code model - generation, plain text representation, and HTML rendering - under a variety of circumstances.

The addition of tests for web application behaviors would be a welcome addition to the test suite in future developments of the project.

### Web application

APIView is an ASP.NET Core project that uses a mix of Razor pages and MVC. User authentication and authorization is done through GitHub, so the application is registered as a GitHub app as well.

#### Configuration

The web application requires a decent bit of configuration to work properly. 

#### GitHub app/authentication

### Azure hosting

APIView is hosted by Azure App Services, under the "APIView" app service in the Azure SDK Developer Playground. For data storage, the project has a storage account - "apiview" - in the same "apiview-rg" resource group. The app uses Azure blob storage for maintaining its database of APIs and comments, which will be further elaborated on below.

#### Storage scheme

Each API in the app is stored as a single blob, and is associated with a separate blob containing all comments on that API. API blobs are stored in a container called "assemblies", and comment blobs are in a container called "comments" - by storing APIs and their comments in separate containers, their blob names can be identical without conflict and make fetching blobs for API review pages simpler.

#### App service

## Areas for future development

There are a number of ways this project can be built upon going forward. An [issue](https://github.com/Azure/azure-sdk-tools/issues/65) on the [azure-sdk-tools](https://github.com/Azure/azure-sdk-tools) repository lists some of these, though many more features could be pursued.

The most significant future improvement to note is the addition of support for other languages, such as Python, Java, and TypeScript. Supporting non-C# languages would require structural changes to the code representation used for this project, as the current types are designed to be specific to C#. This could, however, likely be achieved by more broadly making use of the `TokenApiView` type and making it a base type for all code symbols.
