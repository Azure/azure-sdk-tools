# Contributing to the Pylint Custom Checkers

When contributing a new pylint checker there are three ways to implement it:

1. If the checker has been tested and will have minimal impact within the code and little to no false positives, register your checker and let azure-sdk-for-python know that a new checker will be added with a list of the affected SDKs.

2. If the checker has been tested and will have a great impact within the code and little to no false positives, register your checker and disable the checker globally in the azure-sdk-for-python pylintrc file. This will allow the checker to appear in APIView without breaking any CI pipelines. Code owners will be able to fix the pylint issues without it breaking their code. 

3. If the checker has been tested and has multiple false positives, until the false positives have been corrected, do not register the checker. 