# Release History

## 0.2.0 (Unreleased)

- Checker to enforce docstring keywords being keyword-only in method signature.

## 0.1.0 (2023-08-02)

Add two new checkers:
- Checker to warn against importing the package `six`
- Checker to warn against importing `HttpResponse` from `azure.core.pipeline.transport` 

## 0.0.9 (2023-06-26)
Fix bug with varargs in CheckDocstringParameters checker.
Updated client-paging-methods-use-list checker to include private methods.