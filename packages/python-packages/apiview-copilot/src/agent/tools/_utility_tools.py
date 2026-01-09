# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Plugin for utility functions in the APIView Copilot."""

import asyncio
import json
import os
import tempfile
from urllib.parse import urlparse

import requests
from src._diff import create_diff_with_line_numbers
from src._utils import run_prompty
from src.agent.tools._base import Tool


class UtilityTools(Tool):
    """Utility tools for APIView Copilot."""

    def _download_if_url(self, file_path: str) -> str:
        """
        If file_path is a URL, download it to a temp file and return the local path.
        Otherwise, return the original file_path.
        """
        parsed = urlparse(file_path)
        if parsed.scheme in ("http", "https"):
            response = requests.get(file_path, timeout=30)
            response.raise_for_status()
            suffix = os.path.splitext(parsed.path)[-1]
            with tempfile.NamedTemporaryFile(delete=False, suffix=suffix, mode="wb") as tmp:
                tmp.write(response.content)
                return tmp.name
        return file_path

    def summarize_api(self, api: str, language: str):
        """
        Summarize the provided API content/text. Use this when you have the actual API content
        (not a URL) and want to generate a summary of it.

        :param api: The actual API content/text to summarize (not a URL).
        :param language: The programming language of the API (e.g., 'python', 'java', 'typescript').
        :return: Summary of the API.
        """
        response = run_prompty(
            folder="summarize", filename="summarize_api", inputs={"content": api, "language": language}
        )
        return response

    def summarize_api_diff(self, target: str, base: str, language: str):
        """
        Summarize the differences between two API contents/texts. Use this when you have
        the actual API content (not URLs) for both old and new versions.

        :param target: The target (new) API content/text to compare (not a URL).
        :param base: The base (old) API content/text to compare against (not a URL).
        :param language: The programming language of the APIs (e.g., 'python', 'java', 'typescript').
        :return: Summary of the differences.
        """
        api_diff = create_diff_with_line_numbers(old=base, new=target)
        response = run_prompty(
            folder="summarize", filename="summarize_diff", inputs={"content": api_diff, "language": language}
        )
        return response

    def load_json_file(self, file_path: str):
        """
        Load a JSON file from the specified path or URL.
        :param file_path: The path or URL to the JSON file.
        :return: The JSON content as a pretty-printed string.
        """
        file_path = self._download_if_url(file_path)
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")
        try:
            with open(file_path, "r", encoding="utf-8") as file:
                json_content = json.load(file)
                return json.dumps(json_content, indent=2)
        except json.JSONDecodeError as e:
            raise ValueError(f"Error decoding JSON from file {file_path}.") from e
        except Exception as e:
            raise ValueError(f"Error reading JSON file {file_path}.") from e

    def load_text_file(self, file_path: str):
        """
        Load a text file from the specified path or URL.
        :param file_path: The path or URL to the text file.
        :return: The content of the text file as a string.
        """
        file_path = self._download_if_url(file_path)
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")
        try:
            with open(file_path, "r", encoding="utf-8") as file:
                return file.read()
        except Exception as e:
            raise ValueError(f"Error reading text file {file_path}.") from e

    def parse_apiview_url(self, url: str):
        """
        Parse and extract information from an APIView URL. Use this tool when the user provides
        an apiview.dev URL or asks about a URL containing review IDs or revision IDs.

        Extracts reviewId, revisionId, and optional diffApiRevisionId from APIView URLs.

        :param url: The APIView URL to parse (e.g., https://spa.apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId}&diffApiRevisionId={diffRevisionId})
        :return: JSON string with reviewId, revisionId, and diffApiRevisionId (if present).

        Example:
            url = "https://spa.apiview.dev/review/640b9bc30baa4050a4eccb6c1f6103a7?activeApiRevisionId=176a2e96bc514dcf8f69c8e551e2e124&diffApiRevisionId=0908606abefe463795f647bb184fd1f7"
            Returns: {"reviewId": "640b9bc30baa4050a4eccb6c1f6103a7", "revisionId": "176a2e96bc514dcf8f69c8e551e2e124", "diffApiRevisionId": "0908606abefe463795f647bb184fd1f7"}
        """
        from urllib.parse import parse_qs

        parsed = urlparse(url)
        result = {}

        # Extract reviewId from path (e.g., /review/{reviewId})
        path_parts = parsed.path.strip("/").split("/")
        if len(path_parts) >= 2 and path_parts[0] == "review":
            result["reviewId"] = path_parts[1]

        # Extract revisionId and optional diffApiRevisionId from query parameters
        query_params = parse_qs(parsed.query)
        if "activeApiRevisionId" in query_params:
            result["revisionId"] = query_params["activeApiRevisionId"][0]
        if "diffApiRevisionId" in query_params:
            result["diffApiRevisionId"] = query_params["diffApiRevisionId"][0]

        return json.dumps(result, indent=2)

    def get_apiview_content_from_url(self, url: str):
        """
        Fetch APIView revision content directly from an APIView URL. **USE THIS** when the user
        provides an apiview.dev URL and wants to fetch, view, summarize, or analyze the API content.
        
        This tool extracts the revision ID from the URL and fetches the content in one step,
        which is faster and more reliable than parsing the URL separately.
        
        :param url: The APIView URL (e.g., https://spa.apiview.dev/review/{reviewId}?activeApiRevisionId={revisionId})
        :return: The API revision text content, or an error message
        
        Example:
            url = "https://spa.apiview.dev/review/640b9bc30baa4050a4eccb6c1f6103a7?activeApiRevisionId=176a2e96bc514dcf8f69c8e551e2e124"
            Returns the full API text content for that revision
        """
        from urllib.parse import parse_qs
        from src._apiview import ApiViewClient
        
        parsed = urlparse(url)
        
        # Extract revisionId from query parameter
        query_params = parse_qs(parsed.query)
        revision_id = None
        if "activeApiRevisionId" in query_params:
            revision_id = query_params["activeApiRevisionId"][0]
        
        if not revision_id:
            return json.dumps({"error": "Could not extract activeApiRevisionId from URL. Please check the URL format."})
        
        try:
            client = ApiViewClient()
            content = asyncio.run(client.get_revision_text(revision_id=revision_id))
            return content
        except Exception as e:
            return json.dumps({
                "error": f"Failed to fetch revision content: {str(e)}", 
                "revision_id": revision_id,
                "url": url
            })
