# Azure SDK Pipelines - v3

## Summary

The azure-sdk maintains a common build definition pattern for all our repositories.

```text
<language repo root>
  eng/
    <devops yml>
  sdk/
    keyvault/
      ci.yml <-- recorded tests
      tests.yml <-- livetests
```

A devops build definition exists for each `ci.yml` and `tests.yml` that is present in a given language repo. For the `ci.yml`, there exists an `internal` and `public` version of the build. The former of which is used to **release the package**.

The important detail here is that in most cases, an `internal` and `public` run are **mostly the same** when it comes to the _amount_ of testing that happens.

Let's draw that out using `python core` as an example.

![What tests are repeated?](_imgs/example.png "What is repeated")

As you can see, given there is no intelligence on _what_ to run, we end up running the **full** test suites for all packages for _all_ platforms. This is a ton of redundant testing that _probably_ doesn't catch that many issues.

With this context set, the goal of the `pipelinev3` effort are the following:

- When we are finished, there will be a single build definition triggered on PullRequests.
- The single build definition should dynamically expand and contract _which tests are invoked_ based upon the _context of that PR_.
  - We will need to work with _each language team_ to spec out what is and is not possible based on the language of the package under test. 
- The single build definition should run in _reduced time_ in comparison to the world as-is.

### The Language Matrix

| Criteria                                                           | .NET | Java | JS | Python | go | c | cpp | iOS | android |
|--------------------------------------------------------------------|------|------|----|--------|----|---|-----|-----|---------|
| Can calculate affected packages based on git diff                  |      |      |    |        |    |   |     |     |         |
| Can calculate affected test files based on git diff                |      |      |    |        |    |   |     |     |         |
| Can calculate affected individual tests based on git diff          |      |      |    |        |    |   |     |     |         |
| Can individually run target test files in a PR build               |      |      |    |        |    |   |     |     |         |
| Can individually run target tests within a test file in a PR build |      |      |    |        |    |   |     |     |         |

To support full support a single build for all PRs, a given language must meet all `Criteria`. Only if all are possible will we be able to distribute tests.

### Storing targeted tests during `build` phase?

Depending on how tests must be individually targeted (on a per-language basis), we may need to drop artifacts on disk that can be picked up from each test run. Does this happen during `build` phase?

## `pipeline-agent`

Currently, the sole purpose of this C# binary executable will be evaluate a given PR and generate **json file with a well understood schema**. The json file should contain _at least_ the following:

- The files that have been changed in the PR.
- The target branch and repo
- The source branch and repo

... and other metadata that is useful as we discover it. The above list is not complete.

## `retrieve_test_information.py`

Both Johan and Ben have both mentioned separately that it would be a useful to get an _exact list_ of which tests are succeeding and failing.

IF there is a test that has _only_ succeeded in the past few months...is there a reason that we should run it?

We need to ask _more_ of questions like this.

The scripts present in `data` are intended to retrieve some of this data so we can actually reference it later.

