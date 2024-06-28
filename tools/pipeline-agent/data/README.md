
# Storing targeted tests during `build` phase?

~~Currently, the test data is not available within our data sets. [This issue has been filed](https://github.com/Azure/azure-sdk-tools/issues/4194). `scbedd` will be tackling this.~~

This data is available through `pipeline-witness`. 

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

- `build`: Build Information. We discard this as we can get the build info by joining to the `TestRun` table.
- `testrun`: Which contains details about the testrun which invoked the test. We only retain the Id.
- `lastUpdatedBy`: What touched this testresult? We don't care when reporting on the tests. We will exclude tests by excluding their TestRunId.
- `testCase`: In all explored TestResults, TestCase.Name and TestCaseTitle are the same. We will keep the simple top level value and discard the object.

| Column | Type |
|--------|------|
| TestRunId | `long` |
| TestCaseId | `long` |
| Revision | `int` |
| TestCaseTitle | `string` |
| TestCaseReferenceId | `long` |
| State | `string` |
| Outcome | `string` |
| FailureType | `string` |
| Priority | `int` |
| ComputerName | `string` |
| Url | `string` |
| AutomatedTestName | `string` |
| AutomatedTestStorageName | `string` |
| AutomatedTestType | `string` |
| StartedDate | `DateTime` |
| CompletdDate | `DateTime` |
| LastUpdateDate | `DateTime` |
| CreatedDate | `DateTime` |

> Note! Other samples are available under [sample_test_results.](./data/sample_test_results/)
