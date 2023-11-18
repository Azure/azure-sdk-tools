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

# # Replace these placeholders with your actual values
# organization="your_organization"
# project="your_project"
# runId="your_run_id"
# testCaseResultId="your_test_case_result_id"
# accessToken="your_access_token"
# Make the API request
# curl -H "Authorization: Bearer $accessToken" \
#   "https://dev.azure.com/$organization/$project/_apis/test/Runs/$runId/results/$testCaseResultId?api-version=7.2-preview.6"

import os
import sys
import base64
import json

import httpx

def GET(uri: str):
    pat_bytes = base64.b64encode(bytes(":" + os.getenv('DEVOPS_TOKEN'), 'utf-8'))
    base64_str = pat_bytes.decode('ascii')

    return httpx.get(uri, headers={
        "Authorization": f"Basic {base64_str}"
    })


def get_test_result(organization: str, project: str, run_id: str):
    uri = f"https://dev.azure.com/{organization}/{project}/_apis/test/Runs/{run_id}/results?api-version=7.1-preview.6"
    return GET(uri)


def get_test_results(organization: str, project: str, run_id: str, test_case_result_id: str):
    uri = f"https://dev.azure.com/{organization}/{project}/_apis/test/Runs/{run_id}/results/{test_case_result_id}?api-version=7.1-preview.6"
    return GET(uri)


if __name__ == "__main__":
    if not os.getenv("DEVOPS_TOKEN", None):
        print("This script MUST have access to a valid devops token under environmenet variable DEVOPS_TOKEN. exiting.")
        sys.exit(1)

    result = get_test_result("azure-sdk", "internal", "44348543")
    parsed_result = json.loads(result.text)
    with open('out.json', 'w') as f:
        f.write(result.text)
