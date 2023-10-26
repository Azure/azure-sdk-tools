# Linting the Guidelines

The code presented in this repository is a lift-and-shift of code [originally written by @kristapratico](https://github.com/Azure/azure-sdk-for-python/blob/ae2948416956938c908dcfb570a7456b23482149/scripts/pylint_custom_plugin/)

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
5. If you use the pylint extension for VS code or Pycharm it *should* find the pylintrc automatically.

## How to disable a pylint error

```bash
# pylint:disable=connection-string-should-not-be-constructor-param
```

The pylint custom checkers for SDK guidelines fall into messages range C4717 - C4738.
You will know you came across a custom checker if it contains a link to the guidelines.

In the case of a false positive, use the disable command to remove the pylint error.

## Rules List

| Pylint checker name                                | How to fix this                                                                                                                                                      | How to disable this rule                                                                 | Link to python guideline                                                                      |
|----------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------|
| client-method-should-not-use-static-method         | Use module level functions instead.                                                                                                                                  | # pylint:disable=client-method-should-not-use-static-method                       | [link](https://azure.github.io/azure-sdk/python_implementation.html#method-signatures) |
| missing-client-constructor-parameter-credential    | Add a credential parameter to the client constructor. Do not use plural form "credentials".                                                                          | # pylint:disable=missing-client-constructor-parameter-credential                         | [link](https://azure.github.io/azure-sdk/python_design.html#client-configuration) |
| missing-client-constructor-parameter-kwargs        | Add a **kwargs parameter to the client constructor.                                                                                                                  | # pylint:disable=missing-client-constructor-parameter-kwargs                             | [link](https://azure.github.io/azure-sdk/python_design.html#client-configuration) |
| client-method-has-more-than-5-positional-arguments | Use keyword arguments to reduce number of positional arguments.                                                                                                      | # pylint:disable=client-method-has-more-than-5-positional-arguments                      | [link](https://azure.github.io/azure-sdk/python_implementation.html#method-signatures)          |
| client-method-missing-type-annotations             | Check that param/return type comments are present or that param/return type annotations are present. Check that you did not mix type comments with type annotations. | # pylint:disable=client-method-missing-type-annotations                                  | [link](https://azure.github.io/azure-sdk/python_implementation.html#types-or-not)               |
| client-incorrect-naming-convention                 | Check that you use... snake_case for variable, function, and method names. Pascal case for types. ALL CAPS for constants.                                            | # pylint:disable=client-incorrect-naming-convention                                      | [link](https://azure.github.io/azure-sdk/python_implementation.html#naming-conventions)         |
| client-method-missing-kwargs                       | Check that any methods that make network calls have a **kwargs parameter.                                                                                            | # pylint:disable=client-method-missing-kwargs                                            | [link](https://azure.github.io/azure-sdk/python_design.html#constructors-and-factory-methods) |
| config-missing-kwargs-in-policy                    | Check that the policies in your configuration function contain a **kwargs parameter.                                                                                 | # pylint:disable=config-missing-kwargs-in-policy                                         | [link](https://azure.github.io/azure-sdk/python_design.html#client-configuration) |
| async-client-bad-name                              | Remove "Async" from your service client's name.                                                                                                                      | # pylint:disable=async-client-bad-name                                                   | [link](https://azure.github.io/azure-sdk/python_design.html#async-support)                    |
| file-needs-copyright-header                        | Add a copyright header to the top of your file.                                                                                                                      | # pylint:disable=file-needs-copyright-header                                             | [link](https://azure.github.io/azure-sdk/policies_opensource.html#)                            |
| client-method-name-no-double-underscore            | Don't use method names prefixed with "__".                                                                                                                           | # pylint:disable=client-method-name-no-double-underscore                                 | [link](https://azure.github.io/azure-sdk/python_implementation.html#public-vs-private)          |
| specify-parameter-names-in-call                    | Specify the parameter names when calling methods with more than 2 required positional parameters. e.g. self.get_foo(one, two, three=three, four=four, five=five)     | # pylint:disable=specify-parameter-names-in-call                                         | [link](https://azure.github.io/azure-sdk/python_implementation.html#python-codestyle-positional-params)          |
| connection-string-should-not-be-constructor-param  | Remove connection string parameter from client constructor. Create a method that creates the client using a connection string.                                       | # pylint:disable=connection-string-should-not-be-constructor-param                       | [link](https://azure.github.io/azure-sdk/python_design.html#python-client-connection-string) |
| package-name-incorrect                             | Change your distribution package name to only include dashes, e.g. azure-storage-file-share                                                                          | # pylint:disable=package-name-incorrect                                                  | [link](https://azure.github.io/azure-sdk/python_design.html#packaging)                |
| client-suffix-needed                               | Service client types should use a "Client" suffix, e.g. BlobClient.                                                                                                  | # pylint:disable=client-suffix-needed                                                    | [link](https://azure.github.io/azure-sdk/python_design.html#service-client)                          |
| docstring-admonition-needs-newline                 | Add a blank newline above the .. literalinclude statement.                                                                                                           | # pylint:disable=docstring-admonition-needs-newline                                      | No guideline, just helps our docs get built correctly for microsoft docs.                     |
| naming-mismatch                 | Do not alias models imported from the generated code.                                                                       | # pylint:disable=naming-mismatch                                      | [link](https://github.com/Azure/autorest/blob/main/docs/generate/built-in-directives.md)                     |
| client-accepts-api-version-keyword                | Ensure that the client constructor accepts a keyword-only api_version argument.                                                                       | # pylint:disable=client-accepts-api-version-keyword                                  | [link](https://azure.github.io/azure-sdk/python_design.html#specifying-the-service-version)                     |
| enum-must-be-uppercase                 |  The enum name must be all uppercase.                                                                                                           | # pylint:disable=enum-must-be-uppercase                                      | [link](https://azure.github.io/azure-sdk/python_design.html#enumerations)                     |
| enum-must-inherit-case-insensitive-enum-meta                 |  The enum should inherit from CaseInsensitiveEnumMeta.                                                                                                           | # pylint:disable=enum-must-inherit-case-insensitive-enum-meta                                      | [link](https://azure.github.io/azure-sdk/python_implementation.html#extensible-enumerations)                     |
| networking-import-outside-azure-core-transport                 | This import is only allowed in azure.core.pipeline.transport.                                                                                                          | # pylint:disable=networking-import-outside-azure-core-transport                                     | [link](https://github.com/Azure/azure-sdk-for-python/issues/24989)                     |
| non-abstract-transport-import                      | Only import abstract transports. Let core or end-user decide which transport to use.                                                                                                            | # pylint:disable=non-abstract-transport-import                                     | [link](https://github.com/Azure/azure-sdk-for-python/issues/25533)                     |
| no-raise-with-traceback               | Check that raise_with_traceback from azure-core is replaced with python 3 'raise from' syntax.                                                                                                         | # pylint:disable=no-raise-with-traceback                                     | [link](https://github.com/Azure/azure-sdk-for-python/issues/26759)
| name-too-long                 | Check that the length of class names, function names, and variable names are under 40 characters.                                                                                                            | # pylint:disable=name-too-long                                      |[link](https://github.com/Azure/azure-sdk-for-python/issues/26640)
| delete-operation-wrong-return-type                | Check that delete* or begin_delete* methods return None or LROPoller[None].                                                                                                        | # pylint:disable=delete-operation-wrong-return-type                                      | [link](https://github.com/Azure/azure-sdk-for-python/issues/26662)
client-method-missing-tracing-decorator |pylint:disable=client-method-missing-tracing-decorator       |   Check that sync client methods that make network calls have the sync distributed tracing decorator.                         | [link](https://guidelinescollab.github.io/azure-sdk/python_implementation.html#distributed-tracing)                     |
client-method-missing-tracing-decorator-async | pylint:disable=client-method-missing-tracing-decorator-async    |       Check that async client methods that make network calls have the async distributed tracing decorator.                        | [link](https://guidelinescollab.github.io/azure-sdk/python_implementation.html#distributed-tracing)                     |
client-list-methods-use-paging | pylint:disable=client-list-methods-use-paging    |       Client methods that return collections should use the Paging protocol.                        | [link](https://azure.github.io/azure-sdk/python_design.html#response-formats)           |
docstring-missing-param | pylint:disable=docstring-missing-param    |       Docstring missing for param.                        | [link](https://azure.github.io/azure-sdk/python_documentation.html#docstrings)           |
docstring-missing-type | pylint:disable=docstring-missing-type    |       Docstring missing for param type.                        | [link](https://azure.github.io/azure-sdk/python_documentation.html#docstrings)           |
docstring-missing-return | pylint:disable=docstring-missing-return    |       Docstring missing return.                        | [link](https://azure.github.io/azure-sdk/python_documentation.html#docstrings)           |
docstring-missing-rtype | pylint:disable=docstring-missing-rtype    |       Docstring missing return type.                        | [link](https://azure.github.io/azure-sdk/python_documentation.html#docstrings)           |
docstring-should-be-keyword | pylint:disable=docstring-should-be-keyword    |       Docstring should use keywords.                        | [link](https://azure.github.io/azure-sdk/python_documentation.html#docstrings)           |
do-not-import-legacy-six | pylint:disable=do-not-import-legacy-six    |       Do not import six.                        | No Link.          |
no-legacy-azure-core-http-response-import | pylint:disable=no-legacy-azure-core-http-response-import    |      Do not import HttpResponse from azure.core.pipeline.transport outside of Azure Core. You can import HttpResponse from azure.core.rest instead.               | [link](https://github.com/Azure/azure-sdk-for-python/issues/30785)           |
docstring-keyword-should-match-keyword-only | pylint:disable=docstring-keyword-should-match-keyword-only    |      Docstring keyword arguments and keyword-only method arguments should match.               | [link](https://azure.github.io/azure-sdk/python_documentation.html#docstrings)           |
docstring-type-do-not-use-class | pylint:disable=docstring-type-do-not-use-class   |      Docstring type is formatted incorrectly. Do not use `:class` in docstring type.               | [link](https://sphinx-rtd-tutorial.readthedocs.io/en/latest/docstrings.html)           |
