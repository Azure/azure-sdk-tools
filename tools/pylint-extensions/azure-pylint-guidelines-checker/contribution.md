# Contributing to the Pylint Custom Checkers

The code presented in this repository is a lift-and-shift of code [originally written by @kristapratico](https://github.com/Azure/azure-sdk-for-python/blob/ae2948416956938c908dcfb570a7456b23482149/scripts/pylint_custom_plugin/)

When implementing a new pylint checker there are three cases to consider:

**Checker Scenarios and Corresponding Actions:**

1. If the checker has been tested and will have minimal impact within the code and little to no false positives, register your checker and let azure-sdk-for-python know that a new checker will be added with a list of the affected SDKs.

2. If the checker has been tested and will have a great impact within the code and little to no false positives, register your checker and disable the checker globally in the azure-sdk-for-python pylintrc file. This will allow the checker to appear in APIView without breaking any CI pipelines. Code owners will be able to fix the pylint issues without it breaking their code. 

3. If the checker has been tested and has multiple false positives, until the false positives have been corrected, do not register the checker. 

Note: (Not recommended) If code owners are unable to fix their code in time, with Pylint 2.9.3 there is an ignore-paths option in the pylintrc file. Pass in a regex representation of the paths that should be ignored and create an issue to fix the pylint warnings. 

False Positives     | Impact on code | Action |
|-----------|------------|------------| 
| None/Few      | Low       | Register checker, and inform azure-sdk-for-python of new checker. Note which SDKs will be impacted.         |
| None/Few      | High  | Register checker, and disable it within the azure-sdk-for-python pylintrc file. The checker will appear in APIView and not break CI pipelines.      |            |
| High      | N/A       | Do not register the checker until false positives have been resolved.         |


## Running guidelines checks in Azure/azure-sdk-for-python repository

In order to lint for the guidelines, you must make sure you are enabling the `azure-pylint-guidelines-checker` in your pylint invocation. Using the rcfile at the [root of the repo](https://github.com/Azure/azure-sdk-for-python/blob/main/pylintrc) will ensure that the plugin is properly activated

It is recommended you run pylint at the library package level to be consistent with how the CI runs pylint.

Check that you are running pylint version >=2.14.5 and astroid version >=2.12.0.

0. Install the pylint checker from the azure-sdk development feed.

   ```bash
   pip install --index-url="https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-python/pypi/simple/" azure-pylint-guidelines-checker
   ```

1. Run pylint at the root of the repo and it will automatically find the pylintrc:
   ```bash
   C:\azure-sdk-for-python>pylint sdk/storage/azure-storage-blob/azure
   ```
2. Add the --rcfile command line argument with a relative path to the pylintrc from your current directory:
   ```bash
   C:\azure-sdk-for-python\sdk\storage>pylint --rcfile="../../pylintrc" azure-storage-blob
   ```
3. Set the environment variable PYLINTRC to the absolute path of the pylintrc file:
   ```bash
   set PYLINTRC=C:\azure-sdk-for-python\pylintrc
   ```
   Run pylint:
   ```bash
   C:\azure-sdk-for-python\sdk\storage>pylint azure-storage-blob
   ```
4. Run pylint at the package level using tox and it will find the pylintrc file:
   ```bash
   pip install tox tox-monorepo
   C:\azure-sdk-for-python\sdk\storage\azure-storage-blob>tox -e lint -c ../../../eng/tox/tox.ini
   ```
5. If you use the pylint extension for VS code or Pycharm it _should_ find the pylintrc automatically.
