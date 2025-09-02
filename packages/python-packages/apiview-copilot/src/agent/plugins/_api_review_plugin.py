# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Plugin for performing API reviews using the ApiViewReview class.
"""

import json

from semantic_kernel.functions import kernel_function
from src._apiview_reviewer import ApiViewReview


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
        endpoint = f"/api/apirevisions/{revision_id}/getRevisionText"
        response = await self._http_client.get(endpoint)
        return response.text

    @kernel_function(description="Get the outline for a given API revision")
    async def get_apiview_revision_outline(self, *, revision_id: str) -> str:
        """
        Get the outline for a given API revision.
        Args:
            revision_id (str): The ID of the API revision to retrieve.
        """
        endpoint = f"/api/apirevisions/{revision_id}/outline"
        response = await self._http_client.get(endpoint)
        return response.text

    @kernel_function(description="Retrieves any existing comments for a given API review")
    async def get_apiview_revision_comments(self, *, review_id: str) -> str:
        """
        Get the comments visible for a given API review.
        Args:
            review_id (str): The ID of the API review to retrieve comments for.
        """
        # FIXME: This should target a revision ID, not a review ID
        endpoint = f"/api/comments/{review_id}"
        response = await self._http_client.get(endpoint)
        return response.text
