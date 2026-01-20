# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Tools for performing API reviews using the ApiViewReview class.
"""

import asyncio
import json
from typing import Optional
from urllib.parse import parse_qs, urlparse

from src._apiview import ApiViewClient
from src._apiview_reviewer import ApiViewReview
from src.agent.tools._base import Tool


class ApiReviewTools(Tool):
    """Tools for API review operations."""

    def _parse_apiview_url(self, url: str) -> dict:
        """Extract reviewId and revisionId from an APIView URL."""
        parsed = urlparse(url)
        result = {}

        # Extract reviewId from path (e.g., /review/{reviewId})
        path_parts = parsed.path.strip("/").split("/")
        if len(path_parts) >= 2 and path_parts[0] == "review":
            result["reviewId"] = path_parts[1]

        # Extract revisionId from query parameter
        query_params = parse_qs(parsed.query)
        if "activeApiRevisionId" in query_params:
            result["revisionId"] = query_params["activeApiRevisionId"][0]
        if "diffApiRevisionId" in query_params:
            result["diffApiRevisionId"] = query_params["diffApiRevisionId"][0]

        return result

    def _resolve_revision_id(self, revision_id: Optional[str], url: Optional[str]) -> tuple[Optional[str], Optional[str]]:
        """
        Helper to resolve revision_id from either the ID itself or a URL.
        Returns: (revision_id, error_json)
        If successful, error_json is None.
        If failed, revision_id is None and error_json contains the error message.
        """
        if url:
            parsed = self._parse_apiview_url(url)
            revision_id = parsed.get("revisionId")
            if not revision_id:
                return None, json.dumps({"error": "Could not extract activeApiRevisionId from URL"})

        if not revision_id:
            return None, json.dumps({"error": "Must provide either revision_id or url parameter"})

        return revision_id, None

    def review_api(self, *, language: str, target: str):
        """
        Perform an API review on a single API.
        Args:
            language (str): The programming language of the APIs.
            target (str): The target (new) API to review.
        """
        reviewer = ApiViewReview(target, None, language=language)
        results = reviewer.run()
        return json.dumps(results.model_dump(), indent=2)

    def review_api_diff(self, *, language: str, target: str, base: str):
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

    def get_apiview_revision(self, *, revision_id: Optional[str] = None, url: Optional[str] = None) -> str:
        """
        Get the text of an API revision by revision ID or APIView URL.
        Args:
            revision_id (str, optional): The ID of the API revision to retrieve.
            url (str, optional): The APIView URL containing activeApiRevisionId parameter.
        Note: Provide either revision_id OR url, not both.
        """
        revision_id, error = self._resolve_revision_id(revision_id, url)
        if error:
            return error

        client = ApiViewClient()
        return asyncio.run(client.get_revision_text(revision_id=revision_id))

    def get_apiview_revision_by_review(
        self, *, review_id: Optional[str] = None, url: Optional[str] = None, label: str = "Latest"
    ) -> str:
        """
        Get the text of an API revision by review ID and label, or from an APIView URL.
        Args:
            review_id (str, optional): The ID of the API review to retrieve.
            url (str, optional): The APIView URL to parse for review ID.
            label (str): The label of the API revision to retrieve (default: "Latest").
        Note: Provide either review_id OR url, not both.
        """
        if url:
            parsed = self._parse_apiview_url(url)
            review_id = parsed.get("reviewId")
            if not review_id:
                return json.dumps({"error": "Could not extract reviewId from URL"})

        if not review_id:
            return json.dumps({"error": "Must provide either review_id or url parameter"})

        client = ApiViewClient()
        return asyncio.run(client.get_revision_text(review_id=review_id, label=label))

    def get_apiview_revision_outline(self, *, revision_id: Optional[str] = None, url: Optional[str] = None) -> str:
        """
        Get the outline for a given API revision by revision ID or APIView URL.
        Args:
            revision_id (str, optional): The ID of the API revision to retrieve.
            url (str, optional): The APIView URL containing activeApiRevisionId parameter.
        Note: Provide either revision_id OR url, not both.
        """
        revision_id, error = self._resolve_revision_id(revision_id, url)
        if error:
            return error

        client = ApiViewClient()
        return asyncio.run(client.get_revision_outline(revision_id=revision_id))

    def get_apiview_revision_comments(self, *, revision_id: Optional[str] = None, url: Optional[str] = None) -> str:
        """
        Retrieves any existing comments for a given API revision by revision ID or APIView URL.
        Args:
            revision_id (str, optional): The ID of the API revision to retrieve comments for.
            url (str, optional): The APIView URL containing activeApiRevisionId parameter.
        Note: Provide either revision_id OR url, not both.
        """
        revision_id, error = self._resolve_revision_id(revision_id, url)
        if error:
            return error

        client = ApiViewClient()
        return asyncio.run(client.get_review_comments(revision_id=revision_id))
