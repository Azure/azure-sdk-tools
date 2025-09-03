# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Plugin for performing API reviews using the ApiViewReview class.
"""

import json
from typing import Optional

import requests
from semantic_kernel.functions import kernel_function
from src._apiview_reviewer import ApiViewReview
from src._credential import get_credential

selection_type = {
    "Latest": 1,
    "LatestApproved": 2,
    "LatestAutomatic": 3,
    "LatestManual": 4,
}


class ApiViewClient:
    """Client for interacting with the API View service."""

    def __init__(self):
        self.environment = "staging"

    async def get_revision_text(
        self, *, revision_id: Optional[str] = None, review_id: Optional[str] = None, label: Optional[str] = None
    ) -> str:
        """
        Get the text of an API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
            review_id (str): The ID of the API review to retrieve.
            label (str): Used in conjunction with review_id to specify which revision to get. Defaults to "Latest".
        """
        endpoint = "/api/apirevisions/getRevisionContent?"

        if revision_id:
            if review_id or label:
                raise ValueError("revision_id cannot be used with review_id or label.")
            endpoint += f"apiRevisionId={revision_id}"
        elif review_id:
            if not label:
                label = "Latest"
            if label not in selection_type:
                raise ValueError(f"Invalid label '{label}'. Must be one of {list(selection_type.keys())}.")
            endpoint += f"apiReviewId={review_id}&label={selection_type[label]}"
        else:
            raise ValueError("Either revision_id or review_id must be provided.")
        return await self.send_request(endpoint)

    async def get_revision_outline(self, revision_id: str) -> str:
        """
        Get the outline for a given API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
        """
        return await self.send_request(f"/api/apirevisions/{revision_id}/outline")

    async def get_review_comments(self, revision_id: str) -> str:
        """
        Get the comments visible for a given API review.
        Args:
            revision_id (str): The ID of the API revision to retrieve comments for. Comments that are "visible"
                               from that revision will be returned.
        """
        endpoint = f"/api/comments/getRevisionComments?apiRevisionId={revision_id}"
        return await self.send_request(endpoint)

    async def send_request(self, endpoint: str):
        apiview_endpoints = {
            "production": "https://apiview.dev",
            "staging": "https://apiviewstagingtest.com",
        }
        endpoint_root = apiview_endpoints.get(self.environment)
        endpoint = f"{endpoint_root}/{endpoint}"
        apiview_scopes = {
            "production": "api://apiview/.default",
            "staging": "api://apiviewstaging/.default",
        }
        credential = get_credential()
        scope = apiview_scopes.get(self.environment)
        token = credential.get_token(scope)
        response = requests.get(
            endpoint,
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {token.token}"},
            timeout=30,
        )

        if response.status_code != 200:
            print(f"Error retrieving comments: {response.status_code} - {response.text}")
            return {}
        try:
            return response.json()
        except Exception as json_exc:
            content = response.content.decode("utf-8")
            if "Please login using your GitHub account" in content:
                print("Error: API is still requesting authentication via Github.")
                return {}
            else:
                print(f"Error parsing comments JSON: {json_exc}")
                return {}


class ApiReviewPlugin:
    """Plugin for API review operations."""

    @kernel_function(description="Perform an API review on a single API.")
    async def review_api(self, *, language: str, target: str):
        """
        Perform an API review on a single API.
        Args:
            language (str): The programming language of the APIs.
            target (str): The target (new) API to review.
        """
        reviewer = ApiViewReview(target, None, language=language)
        results = reviewer.run()
        return json.dumps(results.model_dump(), indent=2)

    @kernel_function(description="Perform an API review on a diff between two APIs.")
    async def review_api_diff(self, *, language: str, target: str, base: str):
        """
        Perform an API review on a diff between two APIs.
        Args:
            language (str): The programming language of the APIs.
            target (str): The target (new) API to review.
            base (str): The base (old) API to compare against.
        """
        reviewer = ApiViewReview(target, base, language=language)
        results = reviewer.run()
        return json.dumps(results.model_dump(), indent=2)

    @kernel_function(description="Get the text of an API revision.")
    async def get_apiview_revision(self, *, revision_id: str) -> str:
        """
        Get the text of an API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
        """
        client = ApiViewClient()
        return await client.get_revision_text(revision_id=revision_id)

    @kernel_function(description="Get the text of an API revision by review ID and label.")
    async def get_apiview_revision_by_review(self, *, review_id: str, label: str = "Latest") -> str:
        """
        Get the text of an API revision by review ID and label.
        Args:
            review_id (str): The ID of the API review to retrieve.
            label (str): The label of the API revision to retrieve.
        """
        client = ApiViewClient()
        return await client.get_revision_text(review_id=review_id, label=label)

    @kernel_function(description="Get the outline for a given API revision")
    async def get_apiview_revision_outline(self, *, revision_id: str) -> str:
        """
        Get the outline for a given API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
        """
        client = ApiViewClient()
        return await client.get_revision_outline(revision_id=revision_id)

    @kernel_function(description="Retrieves any existing comments for a given API revision")
    async def get_apiview_revision_comments(self, *, revision_id: str) -> str:
        """
        Get the comments visible for a given API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve comments for.
        """
        client = ApiViewClient()
        return await client.get_review_comments(revision_id=revision_id)
