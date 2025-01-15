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

Install `apiview-stub-generator` package on a supported Python version. If not run on a supported Python version, you may see "Running apiview-stub-generator version ...", even though the tool is not actually running.

From the root of the apiview-stub-generator package, run:
```
pip install . --extra-index-url="https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-python/pypi/simple/"
```

If `--extra-index-url` is not specified, the apiview-stub-generator may not run or you may see the following error:
```
ImportError: cannot import name 'IAstroidChecker' from 'pylint.interfaces
```

Run `apistubgen` command with source repo or whl package as parameter and this will generate a json file with tokens.

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

#### ApiStubGen help and other options

The following options are available when running `apistubgen`:
```
usage: apistubgen [-h] --pkg-path PKG_PATH [--temp-path TEMP_PATH]
                  [--out-path OUT_PATH] [--mapping-path MAPPING_PATH]
                  [--verbose] [--filter-namespace FILTER_NAMESPACE]
                  [--source-url SOURCE_URL] [--skip-pylint]
  -h, --help            show this help message and exit
  --pkg-path PKG_PATH   Path to the package source root, WHL or ZIP
                        file.
  --temp-path TEMP_PATH
                        Extract the package to the specified temporary
                        path. Defaults to a random temp dir.
  --out-path OUT_PATH   Path at which to write the generated JSON file.
                        Defaults to CWD.
  --mapping-path MAPPING_PATH
                        Path to an 'apiview_mapping_python.json' file
                        that supplies cross-language definition IDs.
  --verbose             Enable verbose logging.
  --filter-namespace FILTER_NAMESPACE
                        Generate APIView only for a specific namespace.
  --source-url SOURCE_URL
                        URL to the pull request URL that contains the
                        source used to generate this APIView.
  --skip-pylint         Skips running pylint on the package to obtain
                        diagnostics.
```

### Running tests

```
cd packages/python-packages/apiview-stub-generator
pip install . --extra-index-url="https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-python/pypi/simple/"
pip install -r dev_requirements.txt
pytest tests/
```

#### apistubgentest

The `apistubgentest` package under `packages/python-packages` is used as the source code for testing the `apiview-stub-generator` tool. Classes/functions/etc. in the `apistubgentest` may need to be updated/added to test any new features/bug fixes to the `apiview-stub-generator` tool.

### Upload token file to API review portal
1. Go to ``https://apiview.dev``
2. Click on `Create review`
3. Select `JSON` for `Language`.
4. Select your generated token file and upload.
5. To add a revision, go to `Revisions`, then click `Add revision`.
6. Change language to `JSON`, select the new token file, and upload.

#### Testing in staging API review portal
  1. Go to `https://spa.apiviewstagingtest.com/`
  2. Follow step 2 and on from the `Upload token file to API review portal` section above.
