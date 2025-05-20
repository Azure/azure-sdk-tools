# ContentValidation.Test Project

## Overview

The `ContentValidation.Test` project performs automated test cases to validate the content of SDK documentation, ensuring its accuracy and consistency.

## Test Cases

Each test case reads the `appsettings.json` file to obtain the webpage links to be checked. Then it invokes the corresponding `Validation` methods provided by the `ContentValidation` library to perform validation. Finally, the test results are saved in the `Reports` folder as Excel and JSON files.

- **TestPageAnnotation**
  - **TestMissingTypeAnnotation**: Checks for missing type annotations for classes and method parameters.
- **TestPageLabel**

  - **TestExtraLabel**: Checks for the presence of unnecessary HTML tags on the page.
  - **TestUnnecessarySymbols**: Checks for unnecessary symbols in the page content.
  - **TestInvalidTagsResults**: Check for the presence of invalid html tags in web pages.
  - **TestCodeFormatResults**: Check that the code is formatted correctly in the web page.

- **TestPageContent**
  - **TestTableMissingContent**: Checks for missing content in tables.
  - **TestGarbledText**: Checks for garbled text on the page.
  - **TestDuplicateService**: Checks for duplicate services on the homepage of the SDK documentation.
  - **TestInconsistentTextFormatResults**: Check for inconsistent text is formatted correctly.
