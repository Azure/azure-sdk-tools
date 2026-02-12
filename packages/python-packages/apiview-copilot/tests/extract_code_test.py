# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""
Tests for _extract_code_for_element function in _apiview.py.
"""

import sys
from unittest.mock import MagicMock

# Mock azure.cosmos before importing _apiview
sys.modules["azure.cosmos"] = MagicMock()
sys.modules["azure.cosmos.exceptions"] = MagicMock()

from src._apiview import _extract_code_for_element


class TestExtractCodeForElementEdgeCases:
    """Tests for None/empty inputs."""

    def test_none_full_text_returns_none(self):
        assert _extract_code_for_element(None, "some.element.id") is None

    def test_empty_full_text_returns_none(self):
        assert _extract_code_for_element("", "some.element.id") is None

    def test_none_element_id_returns_none(self):
        assert _extract_code_for_element("1: public class Foo {", None) is None

    def test_empty_element_id_returns_none(self):
        assert _extract_code_for_element("1: public class Foo {", "") is None

    def test_both_none_returns_none(self):
        assert _extract_code_for_element(None, None) is None


class TestExtractCodeForElementMatching:
    """Tests for ElementId matching against APIView text."""

    SAMPLE_TEXT = "\n".join(
        [
            "1: package com.azure.cosmos",
            "2: ",
            "3: public class CosmosClient {",
            "4:     public CosmosClient(String endpoint, TokenCredential credential) {",
            "5:     }",
            "6:     public CosmosDatabase getDatabase(String id) {",
            "7:     }",
            "8:     public void close() {",
            "9:     }",
            "10: }",
        ]
    )

    def test_matches_method_signature(self):
        result = _extract_code_for_element(
            self.SAMPLE_TEXT,
            "com.azure.cosmos.CosmosClient.public-CosmosDatabase-getDatabase(String)",
        )
        assert result is not None
        assert "getDatabase" in result

    def test_matches_class_declaration(self):
        result = _extract_code_for_element(
            self.SAMPLE_TEXT,
            "com.azure.cosmos.public-class-CosmosClient",
        )
        assert result is not None
        assert "CosmosClient" in result

    def test_no_match_returns_element_id_as_fallback(self):
        element_id = "com.azure.cosmos.NonExistentClass.nonExistentMethod()"
        result = _extract_code_for_element(self.SAMPLE_TEXT, element_id)
        # Fallback: returns the element_id itself
        assert result == element_id

    def test_context_lines_default(self):
        """Default context_lines=5 should include surrounding lines."""
        result = _extract_code_for_element(
            self.SAMPLE_TEXT,
            "com.azure.cosmos.CosmosClient.public-void-close()",
        )
        assert result is not None
        # Line 8 matched; context_lines=5 means lines 3–10 should be included
        assert "CosmosClient" in result
        assert "close" in result

    def test_custom_context_lines(self):
        """Requesting context_lines=0 should return only the matched line."""
        result = _extract_code_for_element(
            self.SAMPLE_TEXT,
            "com.azure.cosmos.CosmosClient.public-void-close()",
            context_lines=0,
        )
        assert result is not None
        assert "close" in result
        # Should be just one line
        assert result.count("\n") == 0

    def test_match_at_start_of_text(self):
        """Matching the first line shouldn't cause index errors."""
        result = _extract_code_for_element(
            self.SAMPLE_TEXT,
            "package-com.azure.cosmos",
        )
        assert result is not None
        assert "package" in result

    def test_match_at_end_of_text(self):
        """Matching the last line shouldn't cause index errors."""
        text = "1: public class Foo {\n2: }\n3: // end"
        result = _extract_code_for_element(text, "end", context_lines=0)
        assert result is not None
        assert "end" in result


class TestExtractCodeForElementFormats:
    """Tests for various ElementId formats."""

    def test_maven_style_element_id(self):
        text = "1: com.azure:azure-json:1.5.1"
        element_id = "maven-lineid-properties-com.azure:azure-json:1.5.1"
        result = _extract_code_for_element(text, element_id)
        assert result is not None
        assert "azure-json" in result

    def test_element_id_with_parentheses(self):
        text = "1: public String toString() {"
        result = _extract_code_for_element(
            text,
            "com.azure.cosmos.models.QuantizerType.public-String-toString()",
        )
        assert result is not None
        assert "toString" in result

    def test_single_word_match(self):
        """Even a single-word match should find the best line."""
        text = "1: public enum QuantizerType {\n2:     NONE,\n3:     PRODUCT"
        result = _extract_code_for_element(text, "QuantizerType")
        assert result is not None
        assert "QuantizerType" in result
