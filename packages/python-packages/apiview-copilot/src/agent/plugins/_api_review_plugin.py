# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Plugin for performing API reviews using the ApiViewReview class.
"""

import json
import os

import requests
from semantic_kernel.functions import kernel_function
from src._apiview_reviewer import ApiViewReview
from src._credential import get_credential


class ApiViewClient:
    """Client for interacting with the API View service."""

    def __init__(self):
        self.environment = os.getenv("ENVIRONMENT").lower()

    async def get_revision_text(self, revision_id: str) -> str:
        """
        Get the text of an API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
        """
        response = await self.send_request(f"/api/apirevisions/{revision_id}/getRevisionText")
        return response.text

    async def get_revision_outline(self, revision_id: str) -> str:
        """
        Get the outline for a given API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
        """
        response = await self.send_request(f"/api/apirevisions/{revision_id}/outline")
        return response.text

    async def get_review_comments(self, review_id: str) -> str:
        """
        Get the comments visible for a given API review.
        Args:
            review_id (str): The ID of the API review to retrieve comments for.
        """
        # FIXME: This should target a revision ID, not a review ID
        response = await self.send_request(f"/api/comments/{review_id}")
        return response.text

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
        return client.get_revision_text(revision_id)

    @kernel_function(description="Get the outline for a given API revision")
    async def get_apiview_revision_outline(self, *, revision_id: str) -> str:
        """
        Get the outline for a given API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
        """
        client = ApiViewClient()
        return client.get_revision_outline(revision_id)

    @kernel_function(description="Retrieves any existing comments for a given API review")
    async def get_apiview_revision_comments(self, *, review_id: str) -> str:
        """
        Get the comments visible for a given API review.
        Args:
            review_id (str): The ID of the API review to retrieve comments for.
        """
        client = ApiViewClient()
        return client.get_review_comments(review_id)
