# Content Validation Library

## Overview
The Content Validation library is the core module of this repository, which contains a wealth of rules for document validation of SDK Microsoft Learn Doc in various (python/java/...) languages.

The automated development of text review is a very challenging task. The difficulty lies in the fact that there will be various types of errors, including but not limited to: formatting problems, garbled characters, missing type annotations (only for Python doc), etc.

Therefore, when designing validation rules (usually using regular matching), we will expand the scope of fuzzy matching, which helps us catch more errors. But the flaws are also obvious, and many normal texts will also be included in the error list when matched. Accordingly, we designed the `ignore.json` file, in which we can put the matching content that needs to be filtered out to prevent "exception match".

## Getting started
The current validation rules can fully cover the content validation of [Python SDK Microsoft Learn website](https://learn.microsoft.com/en-us/python/api/overview/azure/?view=azure-python), and some rules can be reused on [Java SDK Microsoft Learn website](https://learn.microsoft.com/en-us/java/api/overview/azure/?view=azure-java). We will develop more rules in the future to meet the content validation of all language SDKs.

For a detailed introduction to the rules, please refer to the following table. You can view the specific design of the rules in the markdown files of the respective languages.

| Languages | Path | Description | 
| ------- | ---- | ----------- |
| Python | [python-rules.md](../docs/rules-introduction/python-rules.md) | content validation rules designed for [Microsoft Learn website](https://learn.microsoft.com/en-us/python/api/overview/azure/?view=azure-python).|
| Java | [java-rules.md](../docs/rules-introduction/java-rules.md) | content validation rules designed for [Microsoft Learn website](https://learn.microsoft.com/en-us/java/api/overview/azure/?view=azure-java).|
| .NET | md | *TODO* |
| JavaScript | md | *TODO* |

>Notes: Currently, the Python and Java rules have been fully developed. The JS rules are being designed and developed. They will be extended to .NET and Go in the future.

## Configuration
You can filter out some unexpected errors by configuring the `ignore.json` file. Compared to hard-coding filter conditions, it is more portable and flexible. Here is an example:

```json
[
    {
        "Rule": "ExtraLabelValidation",
        "IgnoreList": [
            {
                "IgnoreText": "<true",
                "Usage":"",
                "Description": "Example: highlight-<true/false>, '<true' , Link: https://learn.microsoft.com/en-us/python/api/azure-search-documents/azure.search.documents.models.querycaptiontype?view=azure-python"
            }
        ]
    },
    {
        "Rule": "UnnecessarySymbolsValidation",
        "IgnoreList": [
            {
                "IgnoreText": "dist",
                "Usage":"prefix",
                "Description": "Example: dict[str, str] , If the prefix is 'dist' / 'list' / 'optional' ..., the symbol [ is considered meaningful. , Link: https://learn.microsoft.com/en-us/python/api/azure-storage-blob/azure.storage.blob.aio.containerclient?view=azure-python#azure-storage-blob-aio-containerclient-from-connection-string"
            },
            {
                "IgnoreText": "port",
                "Usage":"content",
                "Description": "Example:{protocol}://{fully-qualified-domain-name}[:{port#}] , Link: https://learn.microsoft.com/en-us/python/api/azure-search-documents/azure.search.documents.indexes.models.corsoptions?view=azure-python" 
            }
        ]
    },
    {
        "Rule": "GarbledTextValidation",
        "IgnoreList": [
            {
                "IgnoreText": ":mm:",
                "Usage":"",
                "Description": "specified in the format 'hh:mm:ss', ':mm:' , Link: https://learn.microsoft.com/en-us/python/api/azure-search-documents/azure.search.documents.indexes.models.indexingparametersconfiguration?view=azure-python"
            }
        ]
    }
]
```
>Notes: In the above example, three types of rules are introduced to filter out content during content validation.
>- "**Rule**" - One of the validation rules, where your filter condition needs to be added.
>- "**IgnoreList**" - This is an array of objects in json, each object contains:
>   - "**IgnoreText**" - The content that actually needs to be filtered out.
>   - "**Usage**" - This is an optional option, usually used when there are two or more filtering logics in a rule. In UnnecessarySymbolsValidation, "prefix" means that all content prefixed with IgnoreText will be filtered, and "content" means that all content containing IgnoreText will be filtered.
>   - "**Description**": It is not used in rules code, but only used as a comment in JSON.