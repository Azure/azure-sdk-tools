# Contributing to the Pylint Custom Checkers

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

