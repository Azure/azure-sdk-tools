# ValidationRule.Test Library

## Overview
  The `ValidationRule.Test` library is designed to test whether each validation method in the `ContentValidation` library can accurately detect specific errors in SDK documentation content.  
  
  Whenever a validation method in the `ContentValidation` library is modified, it is essential to ensure the tests in the `ValidationRule.Test` library pass, guaranteeing the robustness and accuracy of the `ContentValidation` library.

## Code Components

- **TestValidations.cs**  
   Reads configuration data from `LocalHtmlData.json`, input the corresponding HTML page paths, then execute the relevant validation methods, and compares the test results with expected results.

- **HTML Folder**  
  Stores HTML files or fragments for testing purposes. These files include various potential error scenarios, enabling validation rules to be tested for their ability to detect these issues accurately.

- **LocalHtmlData.json**  
  The configuration file for test cases. Each test case specifies the validation rule, the HTML file path, and the expected validation results.
 
