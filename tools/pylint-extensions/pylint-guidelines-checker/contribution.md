# Contributing to the Pylint Custom Checkers

When contributing a new pylint checker there are three cases to consider when registering the checker. 

## Checker Scenarios and Corresponding Actions:

1. If the checker has been tested and will have minimal impact within the code and little to no false positives, register your checker and let azure-sdk-for-python know that a new checker will be added with a list of the affected SDKs.

2. If the checker has been tested and will have a great impact within the code and little to no false positives, register your checker and disable the checker globally in the azure-sdk-for-python pylintrc file. This will allow the checker to appear in APIView without breaking any CI pipelines. Code owners will be able to fix the pylint issues without it breaking their code. 

3. If the checker has been tested and has multiple false positives, until the false positives have been corrected, do not register the checker. 

False Positives     | Impact on code | Action |
|-----------|------------|------------| 
| None/Few      | Low       | Register checker, and inform azure-sdk-for-python of new checker. Note which SDKs will be impacted.         |
| None/Few      | High  | Register checker, and disable it within the azure-sdk-for-python pylintrc file. The checker will appear in APIView and not break CI pipelines.      |            |
| High      | N/A       | Do not register the checker until false positives have been resolved.         |

## Status of Current Checkers

| Checker      | Number    | Status    |
|--------------|-----------|-----------|
| missing-client-constructor-parameter-credential | C4717      | Registered,       |
| missing-client-constructor-parameter-kwargs      | C4718  | Registered,   | 
| config-missing-kwargs-in-policy      | C4719  | Registered, |
| unapproved-client-method-name-prefix      | C4720 | Not registered |
| client-method-has-more-than-5-positional-arguments      | C4721  | Registered, |
| client-method-missing-type-annotations      | C4722  | Registered, |
| client-method-missing-tracing-decorator      | C4723  | Not registered |
| client-method-missing-tracing-decorator-async      | C4724  | Not registered |
| client-method-should-not-use-static-method      | C4725 | Registered, |
| file-needs-copyright-header      | C4726  | Registered, |
| client-incorrect-naming-convention      | C4727  | Registered, |
| client-method-missing-kwargs      | C4728  | Registered, |
| client-method-name-no-double-underscore      | C4729  | Registered, |
| client-docstring-use-literal-include      | C4730  | Not registered |
| async-client-bad-name      | C4731  | Registered, |
| specify-parameter-names-in-call      | C4732  | Registered, |
| client-list-methods-use-paging      | C4733  | Not registered |
| client-lro-methods-use-polling      | C4734 | Not registered |
| lro-methods-use-correct-naming      | C4735  | Not registered |
| connection-string-should-not-be-constructor-param      | C4736 | Registered, |
| package-name-incorrect      | C4737  | Registered,  |
| client-suffix-needed      | C4738  | Registered, |
| docstring-missing-param      | C4739  | Registered, Disabled |
| docstring-missing-type      | C4740  | Registered, Disabled |
| docstring-missing-return     | C4741  | Registered, Disabled |
| docstring-missing-rtype      | C4742  | Registered, Disabled |
| docstring-should-be-keyword      | C4743  | Registered, Disabled |
| missing-user-agent-policy      | C4739  | Not registered |
| missing-logging-policy      | C4740 | Not registered |
| missing-retry-policy      | C4741  | Not registered |
| missing-distributed-tracing-policy      | C4742  | Not registered |
| docstring-admonition-needs-newline      | C4744  | Registered, |
| naming-mismatch      | C4745  | Registered |
| enum-must-be-uppercase     | C4746 |  Registered |
| enum-must-inherit-case-insensitive-enum-meta      | C4747  |  Registered |
| client-accepts-api-version-keyword      | C4748  |  Registered |