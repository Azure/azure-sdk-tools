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
| .NET | [dotnet-rules.md](../docs/rules-introduction/dotnet-rules.md) | content validation rules designed for [Microsoft Learn website](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/?view=azure-dotnet).|
| JavaScript | [javascript-rules.md](../docs/rules-introduction/javascript-rules.md) | content validation rules designed for [Microsoft Learn website](https://learn.microsoft.com/en-us/javascript/api/overview/azure/?view=azure-node-latest).|

## Configuration

You can filter out some unexpected errors by configuring the `ignore.json` file. Compared to hard-coding filter conditions, it is more portable and flexible. Here is an example:

```json
[
    {
        "Rule": "CommonValidation",
        "IgnoreList": [
            {
                "IgnoreText": "from_dict",
                "Usage": "contains",
                "Description": "Example: from_dict(data, key_extractors=None, content_type=None) , Link: https://learn.microsoft.com/en-us/python/api/azure-iot-hub/azure.iot.hub.protocol.models.authentication_mechanism.authenticationmechanism?view=azure-python&branch=main"
            },
            {
                "IgnoreText": "\\[\\s*-?\\d+\\s*,\\s*-?\\d+\\s*\\]",
                "Usage": "regular",
                "Description": "Matches expressions like [1, 2] or [-1, 5]"
            }
        ]
    },
    {
        "Rule": "ExtraLabelValidation",
        "IgnoreList": [
            {
                "IgnoreText": "<img",
                "Usage": "contains",
                "Description": "Example: <img src=\"cid:inline_image\"> , Link: https://learn.microsoft.com/en-us/java/api/overview/azure/communication-email-readme?view=azure-java-preview&branch=main"
            }
        ]
    },
    {
        "Rule": "UnnecessarySymbolsValidation",
        "IgnoreList": [
            {
                "IgnoreText": "str",
                "Usage": "before]",
                "Description": "Example: List[str] - filters 'str' when it appears before closing bracket ] , Link: https://learn.microsoft.com/en-us/python/api/azure-keyvault-keys/azure.keyvault.keys.deletedkey?view=azure-python&branch=main"
            },
            {
                "IgnoreText": "list",
                "Usage": "prefix",
                "Description": "Example: list[RecognizedForm] - filters when text starts with 'list' , Link: https://learn.microsoft.com/en-us/python/api/azure-ai-formrecognizer/azure.ai.formrecognizer.aio.formrecognizerclient?view=azure-python"
            },
            {
                "IgnoreText": " str",
                "Usage": "[contains]",
                "Description": "Example: dict[str, str] - filters ' str' within bracket context , Link: https://learn.microsoft.com/en-us/python/api/azure-storage-blob/azure.storage.blob.aio.containerclient?view=azure-python#azure-storage-blob-aio-containerclient-from-connection-string"
            },
            {
                "IgnoreText": "azure.core.",
                "Usage": "<contains>",
                "Description": "Example: <class 'azure.core.pipeline.policies._universal._Unset'> - filters 'azure.core.' within angle brackets , Link: https://learn.microsoft.com/en-us/python/api/azure-core//azure.core.pipeline.policies.requestidpolicy?view=azure-python&branch=main"
            }
        ]
    },
    {
        "Rule": "GarbledTextValidation",
        "IgnoreList": [
            {
                "IgnoreText": ":mm:",
                "Usage": "contains",
                "Description": "specified in the format 'hh:mm:ss', ':mm:' , Link: https://learn.microsoft.com/en-us/python/api/azure-search-documents/azure.search.documents.indexes.models.indexingparametersconfiguration?view=azure-python"
            }
        ]
    },
    {
        "Rule": "TypeAnnotationValidation",
        "IgnoreList": [
            {
                "IgnoreText": "**kwargs",
                "Usage": "equal",
                "Description": "Example: AuthenticationMechanism(**kwargs) - filters when text exactly equals '**kwargs' , Link: https://learn.microsoft.com/en-us/python/api/azure-iot-hub/azure.iot.hub.protocol.models.authentication_mechanism.authenticationmechanism?view=azure-python&branch=main"
            }
        ]
    },
    {
        "Rule": "MissingContentValidation",
        "IgnoreList": [
            {
                "IgnoreText": "message",
                "Usage": "subsetOfErrorClass",
                "Description": "Example: error.message - filters 'message' as a standard error property , Link: https://learn.microsoft.com/en-us/javascript/api/azure-iot-common/errors.argumenterror?view=azure-node-latest&branch=main#constructors"
            },
            {
                "IgnoreText": "reduce",
                "Usage": "subset",
                "Description": "Example: array.reduce() - filters 'reduce' as a standard array method"
            }
        ]
    }
]
```

### Validation Rules and Usage Types

#### 1. CommonValidation

**Purpose**: Filters common programming patterns, standard methods, and mathematical expressions that are legitimate but might be flagged as errors.

**Usage Types**:

- **`contains`** - Filters standard Python methods and common programming terms
  - **When to use**: Common method names that appear anywhere in text (`from_dict`, `serialize`, `deserialize`, `get`, `pop`, `count`, etc.)
  - **Other cases**: Built-in Python methods, dictionary operations, list operations

- **`regular`** - Filters mathematical expressions using regex patterns  
  - **When to use**: Complex patterns like mathematical intervals (`[1, 2]`, `(1, 5]`, `[0.25, 0.75]`)
  - **Other cases**: Coordinate pairs, version ranges, structured data formats

#### 2. ExtraLabelValidation

**Purpose**: Filters legitimate HTML/XML tags and markup that might be incorrectly flagged.

**Usage Types**:

- **`contains`** - Filters HTML/XML tag beginnings
  - **When to use**: HTML or XML tags in documentation (`<img`, `<table`, `<true`)
  - **Other cases**: Configuration placeholders, markup elements

#### 3. UnnecessarySymbolsValidation  

**Purpose**: Filters unnecessary or incorrectly flagged symbols and brackets that appear in legitimate code examples, type definitions, and documentation content.

**Usage Types**:

- **`before]`** - Filters content that appears before closing brackets
  - **When to use**: When legitimate type names or content before `]` are incorrectly flagged as unnecessary symbols (`List[str]`, `Optional[datetime]`)
  - **Other cases**: Generic type parameters, array notation, bracket expressions

- **`prefix`** - Filters content starting with specific keywords that have meaningful brackets
  - **When to use**: When text starting with certain keywords should not have their brackets flagged as unnecessary (`list[RecognizedForm]`, `dict[str, str]`)
  - **Other cases**: Type constructors, method calls with generic parameters

- **`[contains]`** - Filters content within square bracket contexts that are legitimate
  - **When to use**: When content inside square brackets is valid and shouldn't be flagged as unnecessary (`dict[str, str]`, `List[KeyOperation or str]`)
  - **Other cases**: Array indexing, generic type arguments, mathematical notation

- **`<contains>`** - Filters content within angle bracket contexts that are legitimate  
  - **When to use**: When content inside angle brackets is valid code or markup (`List<String>`, `<azure.core.class>`, comparison operators)
  - **Other cases**: Java generics, XML/HTML tags, template syntax, mathematical inequalities

#### 4. GarbledTextValidation

**Purpose**: Filters time formats, timestamps, and special identifiers that might appear as garbled text.

**Usage Types**:

- **`contains`** - Filters time components and protocol identifiers
  - **When to use**: Time formats (`:mm:`, `:05:`), protocol identifiers (`:dtdl:`, `:acs:`)
  - **Other cases**: Timestamp components, service identifiers, namespace separators

#### 5. TypeAnnotationValidation

**Purpose**: Filters Python function signature syntax and parameter annotations.

**Usage Types**:

- **`equal`** - Filters exact matches of syntax elements
  - **When to use**: Exact Python syntax (`**kwargs`, `*args`, `:`, `*`, `/`)
  - **Other cases**: Function parameter syntax, annotation separators

#### 6. MissingContentValidation

**Purpose**: Filters standard error properties and array methods that are expected to exist.

**Usage Types**:

- **`subsetOfErrorClass`** - Filters standard error object properties
  - **When to use**: Standard error properties (`message`, `name`, `stack`)
  - **Other cases**: Exception attributes, error object fields

- **`subset`** - Filters standard array/collection methods
  - **When to use**: Standard array methods (`entries`, `reduce`, `reduceRight`)
  - **Other cases**: Collection operations, prototype methods

### Configuration Structure

- **Rule** - One of the validation rules where your filter condition needs to be added.
- **IgnoreList** - This is an array of objects in JSON, each object contains:
  - **IgnoreText** - The content that actually needs to be filtered out.
  - **Usage** - Defines the filtering logic (see usage types above for detailed explanations)
  - **Description** - Not used in rule code, but only used as a comment in JSON for documentation purposes.

>Notes: If none of the above Usage types apply, you can also design your own Usage and write the necessary filtering conditions in the corresponding rule.