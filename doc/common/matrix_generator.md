# Azure Pipelines Matrix Generator

* [Azure Pipelines Matrix Generator](#azure-pipelines-matrix-generator)
* [How does the matrix generator work](#how-does-the-matrix-generator-work)
* [How to use matrix generator from your pipeline](#how-to-use-matrix-generator-from-your-pipeline)
  * [Matrix generator pipeline usage example](#matrix-generator-pipeline-usage-example)
  * [Runtime matrix generation customization](#runtime-matrix-generation-customization)
* [Matrix config file syntax](#matrix-config-file-syntax)
* [Matrix JSON config fields](#matrix-json-config-fields)
  * [matrix](#matrix)
  * [include](#include)
  * [exclude](#exclude)
  * [displayNames](#displaynames)
  * [$IMPORT](#import)
* [Example matrix generation](#example-matrix-generation)
* [Matrix Generation behavior](#matrix-generation-behavior)
  * [all](#all)
  * [sparse](#sparse)
  * [include/exclude](#includeexclude)
  * [Generated display name](#generated-display-name)
  * [Filters](#filters)
  * [Replace/Modify/Append](#replacemodifyappend-values)
  * [NonSparseParameters](#nonsparseparameters)
  * [Under the hood](#under-the-hood)
* [Testing](#testing)

This directory contains scripts supporting dynamic, cross-product matrix generation for Azure Pipelines jobs.

Azure DevOps supports [multi-job configuration](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/phases?view=azure-devops&tabs=yaml#multi-job-configuration)
via [`jobs.job.strategy.matrix`](https://learn.microsoft.com/en-us/azure/devops/pipelines/yaml-schema/jobs-job-strategy?view=azure-pipelines#strategy-matrix-maxparallel)
definition, but unlike [GitHub's support for job matrixes](https://docs.github.com/en/actions/using-jobs/using-a-matrix-for-your-jobs),
it doesn't allow full cross-product job executions based on the matrix inputs.
This implementation aims to address that, by replicating the cross-product matrix functionality in GitHub actions, together with its
[includes](https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions#jobsjob_idstrategymatrixinclude)
and [excludes](https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions#jobsjob_idstrategymatrixexclude)
filters, but also adds some additional features like sparse matrix generation and programmable matrix filters.

## How does the matrix generator work

This matrix generator implementation works by generating a json value for [`jobs.job.strategy.matrix`](https://learn.microsoft.com/en-us/azure/devops/pipelines/yaml-schema/jobs-job-strategy?view=azure-pipelines#strategy-matrix-maxparallel) and passing it
to the definition, which is possible because [matrix can accept a runtime expression containing a stringified json object](https://docs.microsoft.com/azure/devops/pipelines/process/phases?view=azure-devops&tabs=yaml#multi-job-configuration) (see the code sample at the bottom of the linked section).

You can use the matrix generator in two ways: [from the pipeline](#how-to-use-matrix-generator-from-your-pipeline),
or by calling [`Create-JobMatrix.ps1`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/job-matrix/Create-JobMatrix.ps1) script directly and then passing the generated matrix json as an argument to `jobs.job.strategy.matrix`.

The pipeline usage is recommended.

If you call the generator from the pipeline, you will rely on the [generate template](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/pipelines/templates/jobs/archetype-sdk-tests-generate.yml), which ends up calling `Create-JobMatrix.ps1` behind the scenes.

## How to use matrix generator from your pipeline

Assume you have a job defined in azure pipelines yaml file. You want to run
it in a matrix, leveraging the matrix generator functionality.

To do this, you will need to create another job that will reference your job
definition in its `JobTemplatePath` parameter and generate the matrix
based on one or more matrix json configs referenced in its `MatrixConfigs` parameter.
That job will use as template the definition
[`archetype-sdk-tests-generate.yml`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/pipelines/templates/jobs/archetype-sdk-tests-generate.yml).

### Matrix generator pipeline usage example

Here is an example. Let's assume your job definition has following path:

* [`eng/common-tests/matrix-generator/samples/matrix-job-sample.yml`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common-tests/matrix-generator/samples/matrix-job-sample.yml)

And the path of matrix config you want to use is:

* [`eng/common-tests/matrix-generator/samples/matrix.json`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common-tests/matrix-generator/samples/matrix.json)

If now you want to run the job defined in `matrix-job-sample.yml` in a matrix, with all
the matrix configuration combinations generated with appropriate values by matrix generator
based on `matrix.json`, you need to introduce a job using the `archetype-sdk-tests-generate.yml` template that
will set up your job with the matrix config. It can look like this:

``` yaml
jobs:
  - template: /eng/common/pipelines/templates/jobs/archetype-sdk-tests-generate.yml
    parameters:
      MatrixConfigs:
        - Name: base_product_matrix
          Path: eng/common-tests/matrix-generator/samples/matrix.json
          Selection: all
          NonSparseParameters:
            - framework
          GenerateVMJobs: true
        - Name: sparse_product_matrix
          Path: eng/common-tests/matrix-generator/samples/matrix.json
          Selection: sparse
          GenerateVMJobs: true
      JobTemplatePath: /eng/common-tests/matrix-generator/samples/matrix-job-sample.yml
      AdditionalParameters: []
      CloudConfig:
        SubscriptionConfiguration: $(sub-config-azure-cloud-test-resources)
        Location: eastus2
        Cloud: Public
      MatrixFilters: []
      MatrixReplace: []
      PreGenerationSteps: []
```

To see an example of a complete pipeline definition with a job that runs your job using matrix generator, refer to
[`/eng/common-tests/matrix-generator/samples/matrix-test.yml`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common-tests/matrix-generator/samples/matrix-test.yml).

### Runtime matrix generation customization

The "matrix generator-enabled" job laid out above runs as its own job. A limitation of this approach is that it disallows any runtime matrix customization due to the fact that an individual job clones the targeted build SHA. That is, the matrix to generate would be determined based only on the content of the used matrix json configs.

To address this limitation, we introduce the [stepList](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/templates?view=azure-devops#parameter-data-types) `PreGenerationSteps` as well as `MatrixFilters` and `MatrixReplace`.

`PreGenerationSteps` allows users to update matrix config json however they like prior to actually invoking the matrix generation. Injected steps are run after the repository checkout, but before any matrix generation is invoked.

`MatrixFilters` and `MatrixReplace` allow runtime adjustment of the matrix generation process as can be seen in the source of [GenerateMatrix](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/job-matrix/job-matrix-functions.ps1#L94-L95).  
See also [Filters](#filters) and [Replace/Modify/Append Values](#replacemodifyappend-values).

## Matrix config file syntax

Matrix config file is a [JSON](https://www.json.org) file.

The top-level element in the config is a JSON object having following keys:
`matrix`, `include`, `exclude` and `displayNames`.
For explanation of all the top-level keys, see [Matrix JSON config fields](#matrix-json-config-fields) section below.
Note that `include` and `exclude` have different interpretation than [their
equivalents in GitHub matrix](https://docs.github.com/en/actions/using-jobs/using-a-matrix-for-your-jobs#expanding-or-adding-matrix-configurations).

### Matrix syntax

In `matrix`, each key-value pair denotes a parameter and its values.

Each parameter value is either an array of strings, or an object.

If a parameter value is an object, then we say that parameter represents
a `parameter set group` and the parameter value is a group of all the valid
parameter sets.

The `parameter set group` kind of parameter is useful for when 2 or more parameters
need to be grouped together, but without generating more than one matrix combination.
In such case we group them into one set of given parameter set group.

Grammar of a config file, as an example:

``` yaml
"matrix": {
  "<parameter1 name>": [ <values...> ],
  "<parameter2 name>": [ <values...> ],
  "<parameter set group>": {
    "<parameter set group set 1 name>": {
        "<parameter set 1 key 1": "<value>",
        "<parameter set 1 key 2": "<value>",
    },
    "<parameter set group set 2 name>": {
        "<parameter set group set 2 key 1": "<value>",
        "<parameter set group set 2 key 2": "<value>",
    }
  }
}
"include": [ <matrix>, <matrix>, ... ],
"exclude": [ <matrix>, <matrix>, ... ],
"displayNames": { <parameter value>: <human readable override> },
```

See [`samples/matrix.json`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common-tests/matrix-generator/samples/matrix.json) for a full sample.

## Matrix JSON config fields

### matrix

The `matrix` field defines the base cross-product matrix. The generated matrix can be full or sparse.

Example:

``` json
"matrix": {
  "operatingSystem": [
    "windows-2022",
    "ubuntu-22.04",
    "macos-11"
  ],
  "framework": [
    "net461",
    "netcoreapp2.1",
    "net6.0"
  ],
  "additionalTestArguments": [
    "",
    "/p:UseProjectReferenceToAzureClients=true",
  ]
}
```

### include

The `include` field defines any number of matrix combinations to be appended to the base matrix after processing exclusions.

The value of `include` key is an array of objects, where each object represents one combination to add to all the combinations generated from the matrix.

For example:

``` json
"include": [
    { "param_foo": "foo1","param_bar": "bar3" },
    { "param_foo": "fooA","param_bar": "barC" }
]
```

will add two combinations to the set of output combinations.

There is an alternative, advanced interpretation of `include`, where each object in the array is not one combination
to include, but a matrix used to generate a set of combinations. For example, given:

``` json
"include": [
    {
        "param_foo": ["foo1","foo2"],
        "param_bar": ["bar3","bar4"]
    },
    {
        "param_foo": ["fooA","fooB"],
        "param_bar": ["barC","barD"]
    }
]
```

you will end up including `(2*2)+(2*2)=8` combinations:

``` json
[
  {"param_foo": "foo1", "param_bar": "bar3"},
  {"param_foo": "foo1", "param_bar": "bar4"},
  {"param_foo": "foo2", "param_bar": "bar3"},
  {"param_foo": "foo2", "param_bar": "bar4"},
  {"param_foo": "fooA", "param_bar": "barC"},
  {"param_foo": "fooA", "param_bar": "barD"},
  {"param_foo": "fooB", "param_bar": "barC"},
  {"param_foo": "fooB", "param_bar": "barD"}
]
```

See [`samples/matrix.json`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common-tests/matrix-generator/samples/matrix.json)
for an example of `include` definition.
Note that in this example, because `include[0].TestTargetFramework` parameter value
is an array composed of two elements, the `include` will result in two combinations being added,
differing by the value of `TestTargetFramework`.

Implementation detail: in fact, the simple case of just listing combinations is
a special case of the advanced matrix-generating case, where each array object
has no more than one value for each parameter.

### exclude

The `exclude` field defines any number of matrix combinations to be removed from the base matrix.
`exclude` also supports defining matrix generating these combinations, same way as explained in the section
on `include` above.
Exclude parameters of each generated combination can be a partial set, meaning as long as all exclude parameters
match against a matrix combination (even if the matrix combination has additional parameters),
then it will be excluded from the matrix. For example, the below combination will match the exclusion and be removed:

``` yaml
# An example matrix combination:
{
    "a": 1,
    "b": 2,
    "c": 3,
}

# An example "exclude" with one combination. The combination will match against the combination
# above and exclude it.
{
    "exclude": [
        {
            "a": 1,
            "b": 2
        }
    ]
}
```

### displayNames

Each matrix combination is named and displayed in the Azure pipelines UI by
concatenating values of all the parameters of given combination.
Sometimes these values are too long to be human readable or easy to use,
e.g. as a command line argument.

If this is the case, they can be overridden with `displayNames`. For example:

``` json
"displayNames": {
  "/p:UseProjectReferenceToAzureClients=true": "UseProjectRef"
},
"matrix": {
  "additionalTestArguments": [
    "/p:UseProjectReferenceToAzureClients=true"
  ]
}
```

### $IMPORT

Matrix configs can also import another matrix config. The effect of this is the imported matrix will be generated,
and then the importing config will be combined with that matrix as a product.
Thus, if the imported matrix has `n` combinations and the importing matrix has `m` combinations, the resulting
matrix will have `n * m` combinations.

To import a matrix, add a parameter with the key `$IMPORT`:

``` json
"matrix": {
  "$IMPORT": "path/to/matrix.json",
  "JavaVersion": [ "1.8", "1.11" ]
}
```

Importing can be useful, for example, in cases where there is a shared base matrix, but there is a need to run it
once for each instance of a language version, as seen in the example snippet above.
Importing does not support overriding duplicate parameters.
To achieve this, use the [Replace](#replacemodifyappend-values) argument instead.

The `MatrixConfigs` `Selection` and `NonSparseParameters` parameters are respected when generating an imported matrix.

For an example of how `$IMPORT` works, [Example matrix generation](#example-matrix-generation).

## Example matrix generation

This section shows example matrix generation using the  `matrix`, `include` `exclude` and `$IMPORT` keys
in matrix json config.

Given a matrix and import matrix like below:

``` yaml
# top-level matrix
{
    "matrix": {
        "$IMPORT": "example-matrix.json",
        "endpointType": [ "storage", "cosmos" ],
        "JavaVersion": [ "1.8", "1.11" ]
    },
    "include": [
        {
            "operatingSystem": "windows",
            "mode": "TestFromSource",
            "JavaVersion": "1.8"
        }
    ]
}

# example-matrix.json to import
{
    "matrix": {
      "operatingSystem": [ "windows", "linux" ],
      "client": [ "netty", "okhttp" ]
    },
    "include": [
        {
          "operatingSystem": "mac",
          "client": "netty"
        }
    ]
}
```

1. The base matrix is generated ([sparse](#sparse) in this example):

    ``` yaml
    {
      "storage_18": {
        "endpointType": "storage",
        "JavaVersion": "1.8"
      },
      "cosmos_111": {
        "endpointType": "cosmos",
        "JavaVersion": "1.11"
      }
    }
    ```

1. The imported base matrix is generated ([sparse](#sparse) in this example):

    ``` json
    {
      "windows_netty": {
        "operatingSystem": "windows",
        "client": "netty"
      },
      "linux_okhttp": {
        "operatingSystem": "linux",
        "client": "okhttp"
      }
    }
    ```

1. Includes/excludes from the imported matrix get applied to the imported matrix

    ``` yaml
    {
      "windows_netty": {
        "operatingSystem": "windows",
        "client": "netty"
      },
      "linux_okhttp": {
        "operatingSystem": "linux",
        "client": "okhttp"
      },
      "mac_netty": {
        "operatingSystem": "mac",
        "client": "netty"
      }
    }
    ```

1. The base matrix is multiplied by the imported matrix. In this case, the base matrix has 2 elements,
and the imported matrix has 3 elements, so the product is a matrix with 6 elements:

    ``` yaml
      "storage_18_windows_netty": {
        "endpointType": "storage",
        "JavaVersion": "1.8",
        "operatingSystem": "windows",
        "client": "netty"
      },
      "storage_18_linux_okhttp": {
        "endpointType": "storage",
        "JavaVersion": "1.8",
        "operatingSystem": "linux",
        "client": "okhttp"
      },
      "storage_18_mac_netty": {
        "endpointType": "storage",
        "JavaVersion": "1.8",
        "operatingSystem": "mac",
        "client": "netty"
      },
      "cosmos_111_windows_netty": {
        "endpointType": "cosmos",
        "JavaVersion": "1.11",
        "operatingSystem": "windows",
        "client": "netty"
      },
      "cosmos_111_linux_okhttp": {
        "endpointType": "cosmos",
        "JavaVersion": "1.11",
        "operatingSystem": "linux",
        "client": "okhttp"
      },
      "cosmos_111_mac_netty": {
        "endpointType": "cosmos",
        "JavaVersion": "1.11",
        "operatingSystem": "mac",
        "client": "netty"
      }
    }
    ```

1. Includes/excludes from the top-level matrix get applied to the multiplied matrix, so the below element will be added
   to the above matrix, for an output matrix with 7 elements:

    ``` yaml
    "windows_TestFromSource_18": {
      "operatingSystem": "windows",
      "mode": "TestFromSource",
      "JavaVersion": "1.8"
    }
    ```

## Matrix Generation behavior

### all

`MatrixConfigs.Selection.all` will output the full matrix, i.e. every possible combination of all parameters given.  
The total number of combinations will be: `p1.Length * p2.Length * ... * pn.Length`,  
where `px.Length` denotes the number of values of `x`-th parameter.

### sparse

`MatrixConfigs.Selection.sparse` outputs the minimum number of parameter combinations while ensuring that all parameter values are present in at least one matrix job.
Effectively this means the total number of combinations of a sparse matrix will be equal to the largest matrix
dimension, i.e. `max(p1.Length, p2.Length, ...)`.

To build a sparse matrix, a full matrix is generated, and then walked diagonally N times where N is the largest matrix dimension.
This pattern works for any N-dimensional matrix, via an incrementing index (n, n, n, ...), (n+1, n+1, n+1, ...), etc.
Index lookups against matrix dimensions are calculated modulus the dimension size, so a two-dimensional matrix of 4x2 might be walked like this:

``` yaml
index: 0, 0:
o . . .
. . . .

index: 1, 1:
. . . .
. o . .

index: 2, 2 (modded to 2, 0):
. . o .
. . . .

index: 3, 3 (modded to 3, 1):
. . . .
. . . o
```

### include/exclude

Matrix json configuration `include` and `exclude` keys support additions and subtractions of combinations
off the base matrix.
Both `include` and `exclude` take an array of matrix values. Typically each value will be a single combination,
but `include/exclude` keys also support the cross-product matrix definition syntax of the base matrix.
For details on the matrix generation support, see `Matrix JSON config fields` section for `include`.

Include and exclude are parsed fully. So if a sparse matrix is called for, a sparse version of the base matrix
will be generated, but the full matrix of both include and exclude will be processed.

Excludes are processed first, so includes can be used to forcefully add specific combinations to the matrix,
regardless of exclusions.

### Generated display name

In the matrix job output that azure pipelines consumes, the format is a map of maps. For example:

``` yaml
{
  "net461_macOS1015": {
    "framework": "net461",
    "operatingSystem": "macos-11"
  },
  "net60_ubuntu2204": {
    "framework": "net6.0",
    "operatingSystem": "ubuntu-22.04"
  },
  "netcoreapp21_windows2022": {
    "framework": "netcoreapp2.1",
    "operatingSystem": "windows-2022"
  },
  "UseProjectRef_net461_windows2022": {
    "additionalTestArguments": "/p:UseProjectReferenceToAzureClients=true",
    "framework": "net461",
    "operatingSystem": "windows-2022"
  }
}
```

The top level keys are used as job names, meaning they get displayed in the azure pipelines UI when running the pipeline.

The logic for generating display names works like this:

* Join parameter values by `_`  
    a. If the parameter value exists as a key in [`displayNames`](#displaynames) in the matrix config, replace it with that value.  
    b. For each name value, strip all non-alphanumeric characters (excluding `_`).  
    c. If the name is greater than 100 characters, truncate it.

### Filters

Filters can be passed to the matrix as an array of strings in the `MatrixFilters` parameter,each matching the format of `<key>=<regex>`.
When a matrix combination does not contain the specified key, it will default to a value of empty string for regex parsing.

Filters support filtering for scenarios in which some parameter values are missing. Specifically, you can do the following:

1. filter for combinations that do _not_ have a given parameter;
2. filter for combinations in which when a given parameter with a given key exists, it needs to have a specific value.

Given the filters are regexs, to make these two scenarios possible a missing parameter is treated
as a parameter whose key is present but value is an empty string.  
For an example of case 1., if you want to exclude combinations that do not have parameter named `AbsentParam`
(or, equivalently: you want to include only combinations that do have this parameter), you can create a filter
with regex `AbsentParam=^$`.  
For an example of case 2., if you want to include only combinations that either have `OptionalEnumParam` set to `foo` or `bar`,
or don't have it at all, you can include filter of form `OptionalEnumParam=foo|bar|^$`

Display name filters can also be passed as a single regex string that runs against the [generated display name](#generated-display-name) of the matrix job.
The intent of display name filters is to be defined primarily as a top level variable at template queue time in the azure pipelines UI.
It cannot be passed as parameter to the matrix generator generate template,
[`archetype-sdk-tests-generate.yml`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/pipelines/templates/jobs/archetype-sdk-tests-generate.yml).

For an example of all the various filters in action, the below command will filter for matrix combinations with
`windows` in the job display name, no parameter variable named `ExcludedKey`, a `framework` parameter with value
either `461` or `6.0`, and an optional parameter `SupportedClouds` that, if exists, must contain `Public`:

``` powershell
./Create-JobMatrix.ps1 `
  -ConfigPath samples/matrix.json `
  -Selection all `
  -DisplayNameFilter ".*windows.*" `
  -Filters @("ExcludedKey=^$", "framework=(461|6\.0)", "SupportedClouds=^$|.*Public.*")
```

Note that `Create-JobMatrix.ps1` is [called internally by the generate template](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/pipelines/templates/jobs/archetype-sdk-tests-generate.yml#L72)
and you are never expected to call it directly.
Instead, use the generator from the pipelines, as explained in the [Matrix generator pipeline usage example](#matrix-generator-pipeline-usage-example).

### Replace/Modify/Append Values

Replacements for values can be passed to the matrix as an array of strings in the `MatrixReplace` parameter, each matching the format of `<keyRegex>=<valueRegex>/<replacementValue>`.
The replace argument will find any combinations where the key fully matches the key regex and the value fully matches
the value regex, and replace the value with the replacement specified.

NOTE:

* The replacement value supports regex capture groups, enabling substring transformations, e.g. `Foo=(.*)-replaceMe/$1-replaced`. See the below examples for usage.
* For each key/value pair, the first replacement provided that matches will be the only one applied.
* If `=` or `/` characters need to be part of the regex or replacement, escape them with `\`.

For example, given a matrix config like below:

``` json
{
  "matrix": {
    "Agent": {
      "ubuntu-2204": { "OSVmImage": "ubuntu-22.04", "Pool": "azsdk-pool-mms-ubuntu-2204-general" }
    },
    "JavaTestVersion": [ "1.8", "1.11" ]
  }
}

```

The normal matrix output (without replacements), looks like:

``` powershell
$ ./Create-JobMatrix.ps1 -ConfigPath <test> -Selection all
{
  "ubuntu2204_18": {
    "OSVmImage": "ubuntu-22.04",
    "Pool": "azsdk-pool-mms-ubuntu-2204-general",
    "JavaTestVersion": "1.8"
  },
  "ubuntu2204_111": {
    "OSVmImage": "ubuntu-22.04",
    "Pool": "azsdk-pool-mms-ubuntu-2204-general",
    "JavaTestVersion": "1.11"
  }
}
```

Passing in multiple replacements, the output will look like below. Note that replacing key/value pairs that appear
nested within a grouping will not affect that segment of the job name, since the job takes the grouping name (in this case `ubuntu2204`).

The below example includes samples of regex grouping references, and wildcard key/value regexes:

``` powershell
$ $replacements = @('.*Version=1.11/2.0', 'Pool=(.*ubuntu.*)-general/$1-custom')
$ ../Create-JobMatrix.ps1 -ConfigPath ./test.Json -Selection all -Replace $replacements
{
  "ubuntu2204_18": {
    "OSVmImage": "ubuntu-22.04",
    "Pool": "azsdk-pool-mms-ubuntu-2204-custom",
    "JavaTestVersion": "1.8"
  },
  "ubuntu2204_20": {
    "OSVmImage": "ubuntu-22.04",
    "Pool": "azsdk-pool-mms-ubuntu-2204-custom",
    "JavaTestVersion": "2.0"
  }
}
```

### NonSparseParameters

Sometimes it may be necessary to generate a sparse matrix, but keep the full combination of a few parameters.
The `MatrixConfigs` `NonSparseParameters` parameter allows for more fine-grained control of matrix generation.  
For example:

``` powershell
./Create-JobMatrix.ps1 `
  -ConfigPath /path/to/matrix.json `
  -Selection sparse `
  -NonSparseParameters @("JavaTestVersion")
```

Given a matrix like below with `JavaTestVersion` marked as a non-sparse parameter:

``` json
{
  "matrix": {
    "Agent": {
      "windows-2022": { "OSVmImage": "windows-2022", "Pool": "azsdk-pool-mms-win-2022-general" },
      "ubuntu-2204": { "OSVmImage": "ubuntu-22.04", "Pool": "azsdk-pool-mms-ubuntu-2204-general" },
      "macos-11": { "OSVmImage": "macos-11", "Pool": "Azure Pipelines" }
    },
    "JavaTestVersion": [ "1.8", "1.11" ],
    "AZURE_TEST_HTTP_CLIENTS": "netty",
    "ArmTemplateParameters": [ "@{endpointType='storage'}", "@{endpointType='cosmos'}" ]
  }
}
```

A matrix with 6 combinations will be generated: A sparse matrix of `Agent`, `AZURE_TEST_HTTP_CLIENTS` and `ArmTemplateParameters`
(3 total combinations) will be multiplied by the two `JavaTestVersion` parameter values of `1.8` and `1.11`.

NOTE: `NonSparseParameters` are also applied when generating an imported matrix.

## Under the hood

The [`Create-JobMatrix.ps1`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/job-matrix/Create-JobMatrix.ps1) script generates an N-dimensional matrix with dimensions equal to the parameter array lengths.
For example, the below config would generate a 2x2x1x1x1 matrix (five-dimensional):

``` json
"matrix": {
  "framework": [ "net461", "net6.0" ],
  "additionalTestArguments": [ "", "/p:SuperTest=true" ]
  "pool": [ "ubuntu-22.04" ],
  "container": [ "ubuntu-22.04" ],
  "testMode": [ "Record" ]
}
```

The matrix is stored as a one-dimensional array, with a [row-major](https://wikipedia.org/wiki/Row-_and_column-major_order)
indexing scheme (e.g. `(2, 1, 0, 1, 0)`).

## Testing

The matrix functions can be tested using [pester](https://pester.dev/). The test command must be run from within the tests directory.

``` txt
$ cd <azure sdk tools repo root>/eng/common-tests/matrix-generator/tests
$ Invoke-Pester

Starting discovery in 3 files.
Discovery finished in 75ms.
[+] /home/ben/sdk/azure-sdk-tools/eng/common-tests/matrix-generator/tests/job-matrix-functions.filter.tests.ps1 750ms (309ms|428ms)
[+] /home/ben/sdk/azure-sdk-tools/eng/common-tests/matrix-generator/tests/job-matrix-functions.modification.tests.ps1 867ms (250ms|608ms)
[+] /home/ben/sdk/azure-sdk-tools/eng/common-tests/matrix-generator/tests/job-matrix-functions.tests.ps1 2.71s (725ms|1.93s)
Tests completed in 4.33s
Tests Passed: 141, Failed: 0, Skipped: 4 NotRun: 0
```
