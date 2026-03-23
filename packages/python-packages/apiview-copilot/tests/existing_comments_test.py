# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring,redefined-outer-name,unused-argument,protected-access

"""
Tests for existing comment parsing and validation in ApiViewReview.
"""

import sys
from unittest.mock import MagicMock

import pytest

# Mock azure dependencies before importing
sys.modules["azure.cosmos"] = MagicMock()
sys.modules["azure.cosmos.exceptions"] = MagicMock()
sys.modules["azure.ai.inference"] = MagicMock()
sys.modules["azure.ai.inference.models"] = MagicMock()

from src._apiview_reviewer import ApiViewReview


VALID_COMMENT = {
    "lineNo": 10,
    "createdBy": "azure-sdk",
    "commentText": "This is a comment.",
    "createdOn": "2023-10-01T12:00:00Z",
}


class TestNormalizeCommentKeys:

    def test_maps_author_to_created_by(self):
        data = {"author": "user1", "lineNo": 1, "commentText": "text", "createdOn": "2023-01-01T00:00:00Z"}
        result = ApiViewReview._normalize_comment_keys(data)
        assert result["createdBy"] == "user1"
        assert "author" not in result

    def test_maps_text_to_comment_text(self):
        data = {"text": "some text", "lineNo": 1, "createdBy": "user", "createdOn": "2023-01-01T00:00:00Z"}
        result = ApiViewReview._normalize_comment_keys(data)
        assert result["commentText"] == "some text"
        assert "text" not in result

    def test_maps_timestamp_to_created_on(self):
        data = {"timestamp": "2023-10-01T00:00:00Z", "lineNo": 1, "createdBy": "user", "commentText": "text"}
        result = ApiViewReview._normalize_comment_keys(data)
        assert result["createdOn"] == "2023-10-01T00:00:00Z"
        assert "timestamp" not in result

    def test_leaves_canonical_keys_unchanged(self):
        data = dict(VALID_COMMENT)
        result = ApiViewReview._normalize_comment_keys(data)
        assert result["lineNo"] == 10
        assert result["createdBy"] == "azure-sdk"
        assert result["commentText"] == "This is a comment."
        assert result["createdOn"] == "2023-10-01T12:00:00Z"


class TestParseExistingComments:

    def test_none_returns_empty_list(self):
        assert ApiViewReview._parse_existing_comments(None) == []

    def test_empty_list_returns_empty_list(self):
        assert ApiViewReview._parse_existing_comments([]) == []

    def test_valid_comment_parses_successfully(self):
        result = ApiViewReview._parse_existing_comments([VALID_COMMENT])
        assert len(result) == 1
        assert result[0].line_no == 10
        assert result[0].created_by == "azure-sdk"

    def test_valid_comment_with_snake_case_keys(self):
        data = {
            "line_no": 5,
            "created_by": "user",
            "comment_text": "A note.",
            "created_on": "2024-01-01T00:00:00Z",
        }
        result = ApiViewReview._parse_existing_comments([data])
        assert len(result) == 1
        assert result[0].line_no == 5

    def test_valid_comment_with_alternative_keys(self):
        data = {
            "lineNo": 20,
            "author": "reviewer",
            "text": "A comment.",
            "timestamp": "2024-06-15T10:00:00Z",
        }
        result = ApiViewReview._parse_existing_comments([data])
        assert len(result) == 1
        assert result[0].created_by == "reviewer"
        assert result[0].comment_text == "A comment."

    def test_missing_required_field_raises_value_error(self):
        bad = {
            "createdBy": "azure-sdk",
            "commentText": "text",
            "createdOn": "2023-10-01T12:00:00Z",
            # lineNo missing
        }
        with pytest.raises(ValueError, match="schema did not match expected"):
            ApiViewReview._parse_existing_comments([bad])

    def test_multiple_missing_fields_reported(self):
        bad = {"createdBy": "azure-sdk"}  # missing lineNo, commentText, createdOn
        with pytest.raises(ValueError, match="schema did not match expected"):
            ApiViewReview._parse_existing_comments([bad])

    def test_error_identifies_bad_comment_in_mixed_list(self):
        comments = [
            VALID_COMMENT,
            {"createdBy": "user"},  # bad - missing required fields
        ]
        with pytest.raises(ValueError, match="schema did not match expected"):
            ApiViewReview._parse_existing_comments(comments)

    def test_non_dict_item_raises_value_error(self):
        with pytest.raises(ValueError, match="schema did not match expected"):
            ApiViewReview._parse_existing_comments(["not a dict"])

    def test_multiple_bad_comments_raises_value_error(self):
        comments = [
            {"createdBy": "user"},  # missing 3 fields
            "bad",  # not a dict
        ]
        with pytest.raises(ValueError, match="schema did not match expected"):
            ApiViewReview._parse_existing_comments(comments)

    def test_error_message_includes_schema_hint(self):
        with pytest.raises(ValueError, match="lineNo.*createdBy.*commentText.*createdOn"):
            ApiViewReview._parse_existing_comments([{"bad": "data"}])

    def test_multiple_valid_comments(self):
        comments = [
            VALID_COMMENT,
            {**VALID_COMMENT, "lineNo": 20},
            {**VALID_COMMENT, "lineNo": 30},
        ]
        result = ApiViewReview._parse_existing_comments(comments)
        assert len(result) == 3
        assert [c.line_no for c in result] == [10, 20, 30]
