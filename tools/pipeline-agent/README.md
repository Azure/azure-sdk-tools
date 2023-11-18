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

1. When we are finished, there will be a single build definition triggered on PullRequests.
2. The single build definition should dynamically expand and contract _which tests are invoked_ based upon the _context of that PR_.
3. The single build definition should run in _reduced time_ in comparison to the world as-is.

## `pipeline-agent`

Currently, the sole purpose of this C# binary executable will be evaluate a given PR and generate **json file with a well understood schema**. The json file should contain _at least_ the following:

1. The files that have been changed in the PR.
2. The target branch and repo
3. The source branch and repo

... and other metadata that is useful. This list is by no means complete.

## `retrieve_test_information.py`

Both Johan and Ben have both mentioned separately that it would be a useful to get an _exact list_ of which tests are succeeding and failing.

IF there is a test that has _only_ succeeded in the past few months...is there a reason that we should run it?

We need to ask _more_ of questions like this.

