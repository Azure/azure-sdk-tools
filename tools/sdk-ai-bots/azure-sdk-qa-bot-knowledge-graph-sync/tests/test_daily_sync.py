"""Tests for daily_sync content processing functions.

Ported from the vitest suite (convertMarkdown, preprocessContent,
extractDateFromFilename, extractReleaseInfo, extractSections).
"""

from __future__ import annotations

import pytest

from src.daily_sync import (
    convert_markdown,
    extract_date_from_filename,
    extract_release_info,
    extract_sections,
    preprocess_content,
)


# --- convertMarkdown tests ---


class TestConvertMarkdown:
    def test_frontmatter_title_and_permalink(self):
        md = "---\ntitle: Sample Title\npermalink: custom-name\n---\nHello world."
        result = convert_markdown(md)
        assert result["filename"] == "custom-name"
        assert result["content"].startswith("# Sample Title")
        assert "Hello world." in result["content"]

    def test_no_permalink_empty_filename(self):
        md = "---\ntitle: Sample Title\n---\nHello again."
        result = convert_markdown(md)
        assert result["filename"] == ""
        assert result["content"].startswith("# Sample Title")


# --- extractDateFromFilename tests ---


class TestExtractDateFromFilename:
    def test_valid_release_date(self):
        d = extract_date_from_filename("release-2024-12-25.md")
        assert d == "2024-12-25"

    def test_invalid_filename_returns_epoch(self):
        d = extract_date_from_filename("not-a-release-file.md")
        assert d == "1970-01-01"


# --- preprocessContent tests ---


class TestPreprocessContent:
    def test_escape_backticks(self):
        inp = "Some text\n```\ncode line 1\n```\nMore text"
        result = preprocess_content(inp)
        assert "\\`\\`\\`" in result
        assert "code line 1" in result

    def test_backticks_with_language(self):
        inp = '```python\ndef hello():\n    print("world")\n```'
        result = preprocess_content(inp)
        assert "\\`\\`\\`python" in result

    def test_hash_comments_converted_in_code_blocks(self):
        inp = "```python\n# This is a comment\ndef hello():\n    pass\n```"
        result = preprocess_content(inp)
        assert "// This is a comment" in result
        assert "def hello():" in result

    def test_hash_preserved_outside_code_blocks(self):
        inp = "# Header 1\n## Header 2"
        result = preprocess_content(inp)
        assert "# Header 1" in result
        assert "## Header 2" in result

    def test_inline_hash_preserved(self):
        inp = '```python\n# Start comment\ntext = "value" # inline\n```'
        result = preprocess_content(inp)
        assert "// Start comment" in result
        # Inline # should remain
        assert '# inline' in result

    def test_empty_string(self):
        assert preprocess_content("") == ""

    def test_no_transformations_needed(self):
        inp = "Regular markdown content\nWith **bold** text"
        assert preprocess_content(inp) == inp

    def test_inline_backticks_unchanged(self):
        inp = "Use `inline code` like this"
        assert preprocess_content(inp) == inp


# --- extractReleaseInfo tests ---


class TestExtractReleaseInfo:
    def test_extracts_all_fields(self):
        content = '---\ntitle: "TypeSpec"\nreleaseDate: 2024-03-15\nversion: "0.52.0"\n---\nContent here.'
        info = extract_release_info(content)
        assert info["title"] == "TypeSpec"
        assert info["releaseDate"] == "2024-03-15"
        assert info["version"] == "0.52.0"

    def test_no_frontmatter(self):
        info = extract_release_info("No frontmatter here")
        assert info["title"] == ""
        assert info["releaseDate"] == ""
        assert info["version"] == ""


# --- extractSections tests ---


class TestExtractSections:
    def test_removes_frontmatter_and_downgrades_headers(self):
        content = "---\ntitle: Test\n---\n# Section One\nContent\n## Sub Section"
        result = extract_sections(content)
        assert "---" not in result
        # Headers should be downgraded (one more #)
        assert "## Section One" in result
        assert "### Sub Section" in result

    def test_removes_caution_blocks(self):
        content = "---\ntitle: t\n---\n# H1\n:::caution\nWarning text\n:::\nNormal text"
        result = extract_sections(content)
        assert "caution" not in result
        assert "Normal text" in result
