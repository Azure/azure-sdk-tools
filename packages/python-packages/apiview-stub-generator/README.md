# API Stub Generator [![Build Status](https://dev.azure.com/azure-sdk/public/_apis/build/status/108?branchName=master)](https://dev.azure.com/azure-sdk/public/_build/latest?definitionId=108&branchName=master)

Azure SDK API review tool is used to review all published Azure SDK APIs and this review tool works for all language format. API tool needs a stub file generated in json format with tokens to list APIs in review tool. API stub generator package is developed to create a stub file with api surface level information. Following are the main published components included in this stub file.

    - Classes and module level functions
    - Instance methods
    - Class and Instance variables
    - Properties


## Steps to create API review
Following are the steps to create an API review request for python package.
1. Generate stub file tokens
2. Upload stub file tokens into API review portal

### Generate stub File
`apiview-stub-generator` package is used to generate stub file tokens from either source code repo or from prebuilt wheel package. Following are the steps to generate stub file token.

Install `apiview-stub-generator` package. From the root of the apiview-stub-generator package, run:
```
pip install . --extra-index-url="https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-python/pypi/simple/"
```

Run `apistubgen` command with source repo or whl package as parameter and this will generate a json file with tokens.

#### Help for apistubgen
```
apistubgen --pkg-path <path to package root>
```

This also takes an optional parameter to mention output path where json token file will be generated. If out-path is not given then file will be generated in current working directory.

```
apistubgen --pkg-path <path to package root> --out-path <Outpath path>
```

Sample:
```
apistubgen --pkg-path C:\git\azure-sdk-for-python\sdk\core\azure-core
apistubgen --pkg-path C:\git\azure-sdk-for-python\sdk\core\azure-core --out-path C:\out
```

Token file will be created with a naming convention `<package-name>_python.json'


### Upload token file to API review portal
- Go to ``https://apiview.dev``
- Click on `Create review`
- Select generated token file and upload




