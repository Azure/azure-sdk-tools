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
# Because the test run is super high level, we need to reach out to the devs api to retrieve the actual historical build results. This script is intended to aid in that endeavor

import os
import sys
import base64
import json
import argparse

from typing import List, Set

import httpx
from httpx import Response

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
    # todo, get the min and last updated from the build definition itself
    uri = f"https://vstmr.dev.azure.com/{organization}/{project}/_apis/testresults/runs?minLastUpdatedDate=2023-11-18T00:00:00Z&maxLastUpdatedDate=2023-11-21T00:00:00Z&buildIds={buildid}&api-version=7.2-preview.1"
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


def get_tests_for_build(organization: str, project: str, buildid: str) -> List[Response]:
    test_runs = get_test_runs_for_buildid(organization, project, buildid)
    run_result_sets = []

    for run_id in test_runs:
        run_result_sets.extend(get_test_results(organization, project, run_id))

    return run_result_sets


if __name__ == "__main__":
    if not os.getenv("DEVOPS_TOKEN", None):
        print("This script MUST have access to a valid devops token under environmenet variable DEVOPS_TOKEN. exiting.")
        sys.exit(1)

    # both on azure-sdk internal
    targeted_buildids = [
        "3274085", # storage python
        "3271643"  # core .net
    ]

    for buildid in targeted_buildids:
        results = get_tests_for_build("azure-sdk", "internal", buildid)

        for test_result_response in results:
            parsed_result = json.loads(test_result_response.text)
            with open(f'{buildid}_testrun_{test_result_response}.json', 'w') as f:
                f.write(json.dumps(parsed_result, indent=2))


