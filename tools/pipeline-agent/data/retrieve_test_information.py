# This script REQUIRES the presence of environment variable DEVOPS_TOKEN with the necessary READ permissions.
#
# The data available in the azure sdk kusto cluser related to TestRuns is quite limited,
# but an illustration of what is available is available here:
# https://dataexplorer.azure.com/clusters/azsdkengsys.westus2/databases/Pipelines?query=H4sIAAAAAAAAA12NMQ7CMBAEe15xJUipUARVKMANbcgHjG8hRo4dnc9CSDwep0GCemZ2B2TtS1y96TlCQMfiAxvcfPTqUzwzdR1t23b3NU5pmgMUbKyCDmTvab3nTeVJGELX15/CyK7SWdIDTmnwGtBQPTVF7HJygUuRc/Pb1UQQl0X1E9xoRT928CkvrQAAAA==
#
# Navigate to : https://dataexplorer.azure.com/clusters/azsdkengsys.westus2/databases/Pipelines and invoke
# TestRun
# | where BuildDefinitionId == 2446
# | where CompletedDate > ago(7d)
# | order by CompletedDate desc
# | project Title, RunDurationSeconds, CompletedDate
# | render timechart
#
# Because the test run is super high level, we need to reach out to the devs api to retrieve the actual historical build results. This script is intended to aid in that endeavor, and to be used
# as a reference to implement the upload handler in pipeline-witness.

import os
import sys
import base64
import json
import argparse
import datetime

from typing import List, Set, Any, Dict
from dataclasses import dataclass, asdict

import httpx
from httpx import Response

from azure.storage.blob import BlobServiceClient


@dataclass
class TestResultItem:
    testCaseId: int
    runId: int
    testCaseReferenceId: int
    testCaseTitle: str
    outcome: str
    priority: int
    automatedTestName: str
    automatedTestStorageName: str
    isReRun: bool
    tags: Any
    etlIngestDate: str

def result_from_json_obj(json) -> TestResultItem:
    testCaseId = json.get('id', -1)
    runId = json.get('testRun', {}).get('id', "")
    referenceId = json.get('testCaseReferenceId')
    testCaseTitle = json.get('testCaseTitle')
    outcome = json.get('outcome')
    priority = json.get('priority')
    automatedTestName = json.get('automatedTestName')
    automatedTestStorageName = json.get('automatedTestStorageName')
    isReRun = False
    tags = ['tag1', 'a longer tag 3 that is actually heavy?']
    etlIngestDate = datetime.datetime.now().strftime('%Y-%m-%dT%H:%M:%S.%fZ')
    
    obj = TestResultItem(testCaseId, runId, referenceId, testCaseTitle, outcome, priority, automatedTestName, automatedTestStorageName, isReRun, tags, etlIngestDate)
    return obj


def GET_NO_CONTINUE(uri: str) -> Response:
    pat_bytes = base64.b64encode(bytes(":" + os.getenv('DEVOPS_TOKEN'), 'utf-8'))
    base64_str = pat_bytes.decode('ascii')

    return httpx.get(uri, headers={
        "Authorization": f"Basic {base64_str}"
    })


def GET(uri: str) -> List[Response]:
    response_set = []
    pat_bytes = base64.b64encode(bytes(":" + os.getenv('DEVOPS_TOKEN'), 'utf-8'))
    base64_str = pat_bytes.decode('ascii')
    continue_needed = True
    headers= { "Authorization": f"Basic {base64_str}" }

    # always fire the first request
    response = httpx.get(uri, headers=headers)
    response_set.append(response)

    # now follow continuations if necessary
    while continue_needed:
        if "x-ms-continuationtoken" in response.headers:
            continuation_token = response.headers["x-ms-continuationtoken"]
            continued_uri = f"{uri}&continuationToken={continuation_token}"
            response = httpx.get(continued_uri, headers=headers)
            response_set.append(response)
        else:
            continue_needed = False

    return response_set


def get_test_runs_for_buildid(organization: str, project: str, buildid: str) -> Set[str]:
    # we will have access to build.FinishTime in the BlobUploadProcessor for pipeline-witness, we will use build.FinishTime +- 24 hours to search for the test runs.
    # for this test script, we'll just have a static min and max last updated date
    min_last_updated = "2023-11-16T00:00:00Z"
    max_last_updated = "2023-11-22T00:00:00Z"

    uri = f"https://vstmr.dev.azure.com/{organization}/{project}/_apis/testresults/runs?minLastUpdatedDate={min_last_updated}&maxLastUpdatedDate={max_last_updated}&buildIds={buildid}&api-version=7.2-preview.1"
    test_runs = GET(uri)

    run_set = set()

    for response in test_runs:
        parsed_response = json.loads(response.text)
        run_set.update(set([value["id"] for value in parsed_response["value"]]))

    return run_set


def get_test_results(organization: str, project: str, run_id: str) -> List[Response]:
    uri = f"https://dev.azure.com/{organization}/{project}/_apis/test/Runs/{run_id}/results?api-version=7.1-preview.6"
    return GET(uri)


def get_individual_test_result(organization: str, project: str, run_id: str, test_case_result_id: str) -> Response:
    uri = f"https://dev.azure.com/{organization}/{project}/_apis/test/Runs/{run_id}/results/{test_case_result_id}?api-version=7.1-preview.6"
    return GET(uri)


def get_tests_for_build(organization: str, project: str, buildid: str) -> Dict[str, List[TestResultItem]]:
    test_runs = get_test_runs_for_buildid(organization, project, buildid)
    test_results: Dict[str, List[TestResultItem]] = {}

    for run_id in test_runs:
        if run_id not in test_results:
            test_results[run_id] = []

        test_result_responses = get_test_results(organization, project, run_id)
        for response_blob in test_result_responses:
            results = json.loads(response_blob.text)
            for test_result_blob in results["value"]:
                test_results[run_id].append( 
                    result_from_json_obj(test_result_blob)
                )

    return test_results


def get_project_details(organization: str) -> List[Response]:
    details = GET(f"https://dev.azure.com/{organization}/_apis/projects?api-version=7.2-preview.4")
    return details


def upload_results(test_run_id: str, results: List[TestResultItem]) -> None:
    name = os.getenv("BLOB_ACCOUNT_NAME")
    key = os.getenv("BLOB_ACCOUNT_KEY")
    blob_service_client = BlobServiceClient(account_url=f"https://{name}.blob.core.windows.net", credential=key)
    container_client = blob_service_client.get_container_client('testruns')

    blob_name = f"internal/2023/11/17/{test_run_id}/results.jsonl"

    # {build.Project.Name}/{build.QueueTime:yyyy/MM/dd}/{build.Id}

    # convert to jsonl object
    jsonlString = ""
    for item in results:
        jsonlString += json.dumps(asdict(item)) + "\n"

    json_bytes = jsonlString.encode('utf-8')
    
    # current_datetime.strftime('%Y-%m-%d')
    # blob_client = container_client.get_blob_client(blob_name)

    if not os.path.exists(os.path.dirname(blob_name)):
        os.makedirs(os.path.dirname(blob_name))

    with open(blob_name, 'wb') as f:
        f.write(json_bytes)
        
    # blob_client.upload_blob(json_bytes, blob_type="BlockBlob", content_settings={'content_type': 'application/octet-stream'})


def get_combined_blob_size(buildid: str, testruns: List[str]):
    name = os.getenv("PRIMARY_ACCOUNT_NAME")
    key = os.getenv("PRIMARY_ACCOUNT_KEY")
    blob_service_client = BlobServiceClient(account_url=f"https://{name}.blob.core.windows.net", credential=key)
    testruns_container_client = blob_service_client.get_container_client('testruns')
    build_log_lines_container_client = blob_service_client.get_container_client('buildloglines')
    buildtimelinerecords_container_client = blob_service_client.get_container_client('buildtimelinerecords')
    builds_container_client = blob_service_client.get_container_client('builds')
    print(f"BuildId: {buildid}")
    common_prefix = f"internal/2023/11/17/{buildid}"
    
    # build log line
    total_size_bytes = 0
    count = 0
    build_log_lines_blobs = build_log_lines_container_client.walk_blobs(name_starts_with=common_prefix)
    for blob in build_log_lines_blobs:
        count += 1
        total_size_bytes += blob.size
    print(f"... {total_size_bytes} bytes of build log lines across {count} files.")

    # testrun
    count = 0
    total_size_bytes = 0
    for runid in testruns:
        count+=1
        testrun_blobs = testruns_container_client.walk_blobs(name_starts_with=f"internal/2023/11/17/{runid}")
        for blob in testrun_blobs:
            total_size_bytes += blob.size
    print(f"... {total_size_bytes} bytes of testruns across {count} files.")

    # build timeline record
    count = 0
    total_size_bytes = 0
    timeline_record_blobs = buildtimelinerecords_container_client.walk_blobs(name_starts_with=common_prefix)
    for blob in timeline_record_blobs:
        count+=1
        total_size_bytes += blob.size
    print(f"... {total_size_bytes} bytes of timeline records across {count} files.")

    # builds
    count = 0
    total_size_bytes = 0
    build_blobs = builds_container_client.walk_blobs(name_starts_with=common_prefix)
    for blob in build_blobs:
        count+=1
        total_size_bytes += blob.size
    print(f"... {total_size_bytes} bytes of build blob records across {count} files.")

    breakpoint()

    return total_size_bytes


if __name__ == "__main__":
    if not os.getenv("DEVOPS_TOKEN", None):
        print("This script MUST have access to a valid devops token under environmenet variable DEVOPS_TOKEN. exiting.")
        sys.exit(1)


    details = get_project_details("azure-sdk")

    # all on azure-sdk internal
    targeted_buildids = [
        # "3274085", # storage python (saturday 11/18)
        "3271643",  # core .net (friday 11/17)
        # "3274125", # go azblob (friday 11/17)
        # "3273253", # iOS identity (friday 11/17)
        # "3273729", # java storage (friday 11/17 failed, retried tuesday 11/21)
        # "3273544", # cpp storage (friday 11/17)
        #"3274026", # js storage (friday 11/17 failed)
    ]

    for buildid in targeted_buildids:
        results = get_tests_for_build("azure-sdk", "internal", buildid)
        size = get_combined_blob_size(buildid, results.keys())
        for run_id in results:
            upload_results(run_id, results[run_id])

