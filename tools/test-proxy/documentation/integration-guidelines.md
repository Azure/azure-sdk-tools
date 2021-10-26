# Test-Proxy Integration Guidelines

This document is intended to help users integrate the test-proxy with their specific language, while it is not intended to get into extreme technical details, it will elaborate enough to direct where necessary.

## General Guidance

Every language will need to follow the same basic process.

1. Read the test-proxy readme.
2. Spin up a local version of the test-proxy
3. Transition a single test to flow through the test-proxy
4. Submit a PR
5. Add necessary additions for the test-proxy to start up in CI
6. Update your tests to start the test-proxy automatically
7. Verify in CI
8. Transition the rest of your tests

At any point, when you run into issues, feel free to reach out to `scbedd`. The rest of this document calls out some common features that should be supported by each language's integration with the test-proxy.

## Recommendations for integrating with CI

* Just like your local development experience, start with the choice of "tool" or "docker".. Choose either [test-proxy-docker.yml](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/testproxy/test-proxy-docker.yml) or [test-proxy-tool.yml](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/testproxy/test-proxy-tool.yml).
* To enable `https` to the test-proxy, the certificate `dotnet-devcert.crt` (or `.pfx` if your language requires that) must be trusted.
  * This logic should be implemented, then made callable in Language-Settings.ps1 function 'Import-Dev-Cert-<language>'.
  
## General guidance

- [x] You **should** support using SSL communication with the test-proxy. (Local URL: `https://localhost:5001`)

This can be expensive, as every language needs to trust the https certificate differently. Read [trust-cert-per-language.md](https://github.com/Azure/azure-sdk-tools/blob/main/tools/test-proxy/documentation/trusting-cert-per-language.md) for information about how each language implements this.

- [x] You **should** start a local test server alongside your test run.

Doing so allows you to unit test your language's interactions with the test-proxy itself. At the outset that may not seem relevant, but the _how_ sanitizers and transforms are posted to the test-proxy may be erroneous. It's always good to be certain.

### Implementation Order

- [x] Choose how your team will be running the tool. While it is of course an implemenation detail, 
- [x] Start with manually starting the test proxy. Lean on [docker-start-proxy.ps1]() to understand th

## Test implementation guidance

- [x] You **should** utilize either a custom `transport` or a custom `policy` to redirect traffic to the test-proxy. It completely depends on what your language allows for.

`azure-core` in each language supported by the azure-sdk _normally_ ships with some way to make changes to the requests before they leave the machine. The easiest and most straightforward way to direct traffic to the test-proxy is to exploit these mechanisms.

* JavaScript uses a [custom policy](TODO) for track 2 recordings, and falls back to a [custom httpclient]() for track 1.(Same for performance tests)
* Python uses a [custom policy](https://github.com/Azure/azure-sdk-for-python/blob/main/tools/azure-devtools/src/azure_devtools/perfstress_tests/_policies.py) for performance tests, but uses a lightweight [test decorator](https://github.com/Azure/azure-sdk-for-python/blob/main/tools/azure-sdk-tools/devtools_testutils/proxy_testcase.py#L107) to redirect traffic to the test-proxy during tests.

- [x] You **should** take advantage of the `variables` API to store and retrieve non-secret "random" elements of your tests.

### Session vs Individual Recording Sanitizers
