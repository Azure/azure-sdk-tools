# Azure SDK Pipelines - v3

## State of the world

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

> Note: The azure-sdk-for-net core PR builds use break up the invoked tests across multiple agents. That strategy is the general thrust of `pipelinev3`, just generalized!

## Goals

With the above context set, the goal of the `pipelinev3` effort are the following:

- When we are finished, there will be a single build definition triggered on PullRequests.
- The single build definition should dynamically expand and contract _which tests are invoked_ based upon the _context of that PR_.
  - We will need to work with _each language team_ to spec out what is and is not possible based on the language of the package under test. 
- The single build definition should run in _reduced time_ in comparison to the world as-is.
- We will be able to track the success or failure of a specific test over time.
  - This data will be utilized to further reduce the tests run during PRs or CI.

### Optional / Supporting Efforts - Storing targeted tests during `build` phase?

Currently, the test data is not available within our data sets. [This issue has been filed](https://github.com/Azure/azure-sdk-tools/issues/4194). `scbedd` will be tackling this.

#### `retrieve_test_information.py`

Both Johan and Ben have both mentioned separately that it would be a useful to get an _exact list_ of which tests are succeeding and failing.

IF there is a test that has _only_ succeeded in the past few months...is there a reason that we should run it? We need to ask _more_ of questions like this.

The scripts present in `data` are exploratory and used to investigate what data is available without attempting to integrate it with `pipeline-witness`. `pipeline-witness` is triggered by `pipeline-completed` events from github. As we process the metadata for a completed build, the `lastUpdatedTime` will be available. This date, plus or minus a couple days will serve as the `minLastUpdatedDate` and `maxLastUpdatedDate` when retrieving TestRuns for a given buildId.

Once we've retrieved the buildId, we will pull down the individual testResults for that runId. This is a couple sample test results:

```jsonc
// individual test result from a python storage run
{
    "id": 100000,
    "project": {
        "id": "590cfd2a-581c-4dcb-a12e-6568ce786175",
        "name": "internal",
        "url": "https://dev.azure.com/azure-sdk/_apis/projects/internal"
    },
    "startedDate": "2023-11-18T01:37:20.947Z",
    "completedDate": "2023-11-18T01:38:01.35Z",
    "durationInMs": 40403.0,
    "outcome": "Passed",
    "revision": 1,
    "state": "Completed",
    "testCase": {
        "name": "test_create_blob"
    },
    "testRun": {
        "id": "44424448",
        "name": "storage  Test windows2022_39_7",
        "url": "https://dev.azure.com/azure-sdk/internal/_apis/test/Runs/44424448"
    },
    "lastUpdatedDate": "2023-11-18T01:56:33.863Z",
    "priority": 0,
    "computerName": "853f4eb1c00000E",
    "build": {
        "id": "3274085",
        "name": "20231117.1",
        "url": "https://dev.azure.com/azure-sdk/_apis/build/Builds/3274085"
    },
    "createdDate": "2023-11-18T01:56:33.863Z",
    "url": "https://dev.azure.com/azure-sdk/internal/_apis/test/Runs/44424448/Results/100000",
    "failureType": "None",
    "automatedTestStorage": "sdk.storage.azure-storage-blob.tests.test_append_blob.TestStorageAppendBlob",
    "automatedTestType": "JUnit",
    "testCaseTitle": "test_create_blob",
    "customFields": [],
    "testCaseReferenceId": 255381332,
    "lastUpdatedBy": {
        "displayName": "internal Build Service (azure-sdk)",
        "url": "https://spsprodcus3.vssps.visualstudio.com/A207f0a5c-e100-4304-88d7-ea33d810297c/_apis/Identities/429d39d2-8bab-45b8-bbea-f79e43191f9b",
        "_links": {
            "avatar": {
                "href": "https://dev.azure.com/azure-sdk/_apis/GraphProfile/MemberAvatars/svc.MjA3ZjBhNWMtZTEwMC00MzA0LTg4ZDctZWEzM2Q4MTAyOTdjOkJ1aWxkOjU5MGNmZDJhLTU4MWMtNGRjYi1hMTJlLTY1NjhjZTc4NjE3NQ"
            }
        },
        "id": "429d39d2-8bab-45b8-bbea-f79e43191f9b",
        "uniqueName": "Build\\590cfd2a-581c-4dcb-a12e-6568ce786175",
        "imageUrl": "https://dev.azure.com/azure-sdk/_apis/GraphProfile/MemberAvatars/svc.MjA3ZjBhNWMtZTEwMC00MzA0LTg4ZDctZWEzM2Q4MTAyOTdjOkJ1aWxkOjU5MGNmZDJhLTU4MWMtNGRjYi1hMTJlLTY1NjhjZTc4NjE3NQ",
        "descriptor": "svc.MjA3ZjBhNWMtZTEwMC00MzA0LTg4ZDctZWEzM2Q4MTAyOTdjOkJ1aWxkOjU5MGNmZDJhLTU4MWMtNGRjYi1hMTJlLTY1NjhjZTc4NjE3NQ"
    },
    "automatedTestName": "test_create_blob"
}
```

```jsonc
// individual test result from .NET core test
{
  "id": 100000,
  "project": {
    "id": "590cfd2a-581c-4dcb-a12e-6568ce786175",
    "name": "internal",
    "url": "https://dev.azure.com/azure-sdk/_apis/projects/internal"
  },
  "startedDate": "2023-11-17T09:10:03.47Z",
  "completedDate": "2023-11-17T09:10:03.47Z",
  "outcome": "Passed",
  "revision": 1,
  "state": "Completed",
  "testCase": {
    "name": "CanRoundTripValueBodyMessages(3.1415926d)"
  },
  "testRun": {
    "id": "44405954",
    "name": "Windows net7.0",
    "url": "https://dev.azure.com/azure-sdk/internal/_apis/test/Runs/44405954"
  },
  "lastUpdatedDate": "2023-11-17T09:13:05.203Z",
  "priority": 255,
  "computerName": "7e60d6cbc000003",
  "build": {
    "id": "3271643",
    "name": "20231117.1",
    "url": "https://dev.azure.com/azure-sdk/_apis/build/Builds/3271643"
  },
  "createdDate": "2023-11-17T09:13:05.203Z",
  "url": "https://dev.azure.com/azure-sdk/internal/_apis/test/Runs/44405954/Results/100000",
  "failureType": "None",
  "automatedTestStorage": "azure.core.amqp.tests.dll",
  "automatedTestType": "UnitTest",
  "testCaseTitle": "CanRoundTripValueBodyMessages(3.1415926d)",
  "customFields": [],
  "testCaseReferenceId": 456716594,
  "lastUpdatedBy": {
    "displayName": "internal Build Service (azure-sdk)",
    "url": "https://spsprodcus3.vssps.visualstudio.com/A207f0a5c-e100-4304-88d7-ea33d810297c/_apis/Identities/429d39d2-8bab-45b8-bbea-f79e43191f9b",
    "_links": {
      "avatar": {
        "href": "https://dev.azure.com/azure-sdk/_apis/GraphProfile/MemberAvatars/svc.MjA3ZjBhNWMtZTEwMC00MzA0LTg4ZDctZWEzM2Q4MTAyOTdjOkJ1aWxkOjU5MGNmZDJhLTU4MWMtNGRjYi1hMTJlLTY1NjhjZTc4NjE3NQ"
      }
    },
    "id": "429d39d2-8bab-45b8-bbea-f79e43191f9b",
    "uniqueName": "Build\\590cfd2a-581c-4dcb-a12e-6568ce786175",
    "imageUrl": "https://dev.azure.com/azure-sdk/_apis/GraphProfile/MemberAvatars/svc.MjA3ZjBhNWMtZTEwMC00MzA0LTg4ZDctZWEzM2Q4MTAyOTdjOkJ1aWxkOjU5MGNmZDJhLTU4MWMtNGRjYi1hMTJlLTY1NjhjZTc4NjE3NQ",
    "descriptor": "svc.MjA3ZjBhNWMtZTEwMC00MzA0LTg4ZDctZWEzM2Q4MTAyOTdjOkJ1aWxkOjU5MGNmZDJhLTU4MWMtNGRjYi1hMTJlLTY1NjhjZTc4NjE3NQ"
  },
  "automatedTestName": "Azure.Core.Amqp.Tests.AmqpAnnotatedMessageConverterTests.CanRoundTripValueBodyMessages(3.1415926d)"
}
```

From which we we will produce the `TestResults` table.

We discard:

- Build Information (available through TestRunId -> TestRun -> BuildId -> Build via join)
- Test Run Information (aside from TestRunId to map to TestRun table)
- `Last Updated By` object information...not useful for test results

| Column | Type | Accessor/Notes |
|--------|------|----------|
| TestRunId | `long` | |
| TestCaseId | `long` | |
| StartedDate | `DateTime` | |
| CompletdDate | `DateTime` | |
| LastUpdateDate | `DateTime` | |
| CreatedDate | `DateTime` | |
| Outcome | `string` | |
| Revision | `int` | |
| State | `string` | |
| TestCase | `string` | TestCase.Name |
| TestCaseTitle | `string` | If TestCase.Name and TestCaseTitle are _always_ the same, we should dump one. |
| Priority | `int` | |
| ComputerName | `string` | |
| Url | `string` | |
| FailureType | `string` | |
| TestCaseReferenceId | `long` | |
| AutomatedTestName | `string` | |
| AutomatedTestStorageName | `string` | |
| AutomatedTestType | `string` | |

## The Language Matrix - How will work be parceled out?

| Criteria                                                           | .NET | Java | JS | Python | Go | c | cpp | iOS | Android |
|--------------------------------------------------------------------|------|------|----|--------|----|---|-----|-----|---------|
| Can calculate affected packages based on git diff                  |      |      |    |        |    |   |     |     |         |
| Can calculate affected test files based on git diff                |      |      |    |        |    |   |     |     |         |
| Can calculate affected individual tests based on git diff          |      |      |    |        |    |   |     |     |         |
| Can individually run target test files in a PR build               |      |      |    |        |    |   |     |     |         |
| Can individually run target tests within a test file in a PR build |      |      |    |        |    |   |     |     |         |

To support full support a single build for all PRs, we need a strategy to distribute individual tests to a specific platform. Only if all are possible will we be able to distribute tests.

Depending on how tests must be individually targeted (on a per-language basis), we may need to drop artifacts on disk that can be picked up from each test run. Does this happen during `build` phase? details present for each language below.

### .NET

### Java

### JS

### Python

### Go

### c

### cpp

### iOS

### Android

## `pipeline-agent`

Currently, the sole purpose of this C# binary executable will be evaluate a given PR and generate **json file with a well understood schema**. The json file should contain _at least_ the following:

- The files that have been changed in the PR.
- The target branch and repo
- The source branch and repo

... and other metadata that is useful as we discover it. The above list is not complete.
