## Overview
This folder contains a collection of tools that utilize AI techniques.

#### AzureSdkQaBot
AzureSdkQaBot is a Teams bot which can answer the questions related to the Azure SDK domain. It is powered by Azure OpenAI and written in C#.

**Capability of this bot**

-	This bot can provide general QA by searching through a knowledge base that includes documents from three sources:
    - Azure SDK documents available at the [engineering hub site](https://eng.ms/docs/products/azure-developer-experience)
    - TypeSpec documents available at https://github.com/azure/typespec-azure
    - Some markdown documents from a [customized list](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/Embeddings/settings/metadata_customized_docs.json)

-	Assist with reviewing pull requests for REST API specifications, including query the status of the specification pull request, providing suggestions for next steps, and facilitating the merge process (note that merging is disabled during testing phase)

**How to use this bot**
-	Create a post in the teams channel by accessing https://aka.ms/azsdk/teams-bot.
-	In the post content, please include `@AzureSDKAssistant-dev` with your questions. 
> Note: Select the name when typing `@`, copying of the `@AzureSDKAssistant-dev` text to the question would not work.


#### Embeddings
It is a tool written in Python that uses `langchain` library to create embeddings in Azure Search Service.

#### Scripts
This folder contains some scripts to build embeddings by calling the `Embeddings` tool.


## How to Refresh the Document Embeddings Used by Teams Bot
We have an [Azure DevOps pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=6811&_a=summary) which can help create or refresh the embeddings. 

1. This pipeline contains three stages:
    - Build EngHub Document Embeddings
    This stage builds embeddings for all the documents under the [engineering hub site](https://dev.azure.com/azure-sdk/internal/_git/azure-sdk-docs-eng.ms?path=/docs)
    - Build TypeSpec Document Embeddings
    This stage builds embeddings for all the documents under the [typespec-azure site](https://github.com/Azure/typespec-azure)
    - Build Customized Document Embeddings
    This stage builds embeddings for some markdown documents which are publicly accessible. 

2. The user can select specific stages when running the pipeline. By default, all three stages are included.

3. The pipeline has an option to refresh the embeddings incrementally. By default, `Incremental Embedding Build` is selected when the pipeline is triggered. If the user wants to create embeddings from scratch, they should unselect this option when triggering the pipeline.

### How to Add a New Document to the Customized Document List
If you have a publicly accessbile markdown file that you want the Teams bot to understand, you can add the information to [this file](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/Embeddings/settings/metadata_customized_docs.json) in the following format.
```JSON
"ci-fix.md": {
    "title": "CI Fix Guide",
    "url": "https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/ci-fix.md"
  }
```
 This file is a `JSON`, and you must ensure that the `key` in this `JSON` is not duplicated when adding a new document.