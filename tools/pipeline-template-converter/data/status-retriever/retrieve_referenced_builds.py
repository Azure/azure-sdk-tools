# This script REQUIRES the presence of environment variable DEVOPS_TOKEN with the necessary READ permissions.

import os
import sys
import base64
import json
import csv

from typing import List, Set, Any, Dict
from dataclasses import dataclass, asdict

import httpx
from httpx import Response

from azure.storage.blob import BlobServiceClient

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

def get_test_results(organization: str, project: str, run_id: str) -> List[Response]:
    uri = f"https://dev.azure.com/{organization}/{project}/_apis/test/Runs/{run_id}/results?api-version=7.1-preview.6"
    return GET(uri)


def get_individual_test_result(organization: str, project: str, run_id: str, test_case_result_id: str) -> Response:
    uri = f"https://dev.azure.com/{organization}/{project}/_apis/test/Runs/{run_id}/results/{test_case_result_id}?api-version=7.1-preview.6"
    return GET(uri)


def get_project_details(organization: str) -> List[Response]:
    details = GET(f"https://dev.azure.com/{organization}/_apis/projects?api-version=7.2-preview.4")
    return details

def get_build_definitions(organization: str, project: str) -> List[Response]:
    uri = f"https://dev.azure.com/{organization}/{project}/_apis/build/definitions?api-version=7.2-preview.7"
    return GET(uri)[0]

def get_yaml_from_build_definition(organization: str, project: str, definition_id: str) -> Response:
    uri = f"https://dev.azure.com/{organization}/{project}/_apis/build/definitions/{definition_id}?api-version=7.1"
    return GET(uri)[0]


if __name__ == "__main__":
    if not os.getenv("DEVOPS_TOKEN", None):
        print("This script MUST have access to a valid devops token under environmenet variable DEVOPS_TOKEN. exiting.")
        sys.exit(1)

    project = "internal"
    organization = "azure-sdk"
    yaml_build_references = []
    designer_build_references = []
    datafile = f"{project}_build_definitions.json"

    if not os.path.exists(datafile):
        try:
            all_build_definitions = json.loads(get_build_definitions(organization, project).text)

            total_definition_count = len(all_build_definitions['value'])

            for idx, buildDefinitionDetail in enumerate(all_build_definitions["value"]):
                definition_id = str(buildDefinitionDetail["id"])
                definition_name = str(buildDefinitionDetail["name"])
                detailed_build_def = json.loads(get_yaml_from_build_definition(organization, project, definition_id).text)
                try:
                    if "yamlFilename" in detailed_build_def["process"]:
                        referenced_yml_file = detailed_build_def["process"]["yamlFilename"]
                    else:
                        referenced_yml_file = "designer build"
                except Exception as f:
                    breakpoint()
                uri = detailed_build_def["_links"]["web"]["href"]
                print(f"Retrieved details for {definition_name}. {idx}/{total_definition_count}.")
                yaml_build_references.append([project, definition_id, definition_name, referenced_yml_file, uri])
        finally:
            if yaml_build_references:
                with open(datafile, 'w', newline='') as csvfile:
                    spamwriter = csv.writer(csvfile)
                    for row in yaml_build_references:
                        spamwriter.writerow(row)

    