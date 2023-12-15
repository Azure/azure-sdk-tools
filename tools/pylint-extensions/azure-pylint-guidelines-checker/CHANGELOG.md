# Release History

## 0.3.0 (2023-12-15)

- Breaking changes involved in bump to pylint 3.0 support and bug fix to `incorrect-naming-convention` checker

## 0.2.0 (2023-10-17)

- Checker to enforce docstring keywords being keyword-only in method signature.
- Fixed a bug in `no-legacy-azure-core-http-response-import` that was throwing warnings for azure-mgmt-core.

## 0.1.0 (2023-08-02)

Add two new checkers:
- Checker to warn against importing the package `six`
- Checker to warn against importing `HttpResponse` from `azure.core.pipeline.transport` 

## 0.0.9 (2023-06-26)
Fix bug with varargs in CheckDocstringParameters checker.
Updated client-paging-methods-use-list checker to include private methods.