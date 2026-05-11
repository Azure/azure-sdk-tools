# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Unit tests for the guideline ingestor module.

Covers:
- Markdown parsing (Jekyll tag replacement, ID extraction, BeautifulSoup parsing)
- Content hashing (normalization, stability)
- ID format validation
- Deduplication logic
"""

# pylint: disable=protected-access

from src._guideline_ingestor import (
    GuidelineIngestor,
    ParsedGuideline,
)


# ============================================================================
# Markdown Parsing Tests
# ============================================================================


class TestJekyllTagReplacement:
    """Tests for Jekyll requirement tag replacement via _split_tags."""

    @staticmethod
    def _make_soup_element(html_text):
        """Create a BeautifulSoup element from HTML text."""
        from bs4 import BeautifulSoup

        soup = BeautifulSoup(html_text, "html.parser")
        return soup.find()

    def test_must_tag_replaced(self):
        html = '<p>{% include requirement/MUST id="test-id" %} use HTTPS for all requests.</p>'
        elem = self._make_soup_element(html)
        text, gid = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert "DO use HTTPS" in text
        assert "{% include" not in text
        assert gid == "python_design=html=test-id"

    def test_must_not_tag_replaced(self):
        html = '<p>{% include requirement/MUSTNOT id="no-sync" %} expose synchronous APIs.</p>'
        elem = self._make_soup_element(html)
        text, gid = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert "DO NOT expose" in text
        assert gid == "python_design=html=no-sync"

    def test_should_tag_replaced(self):
        html = '<p>{% include requirement/SHOULD id="use-enums" %} prefer enums over strings.</p>'
        elem = self._make_soup_element(html)
        text, gid = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert "YOU SHOULD prefer" in text
        assert gid == "python_design=html=use-enums"

    def test_should_not_tag_replaced(self):
        html = '<p>{% include requirement/SHOULDNOT id="no-globals" %} use global state.</p>'
        elem = self._make_soup_element(html)
        text, gid = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert "YOU SHOULD NOT use" in text
        assert gid == "python_design=html=no-globals"

    def test_may_tag_replaced(self):
        html = '<p>{% include requirement/MAY id="opt-cache" %} cache responses.</p>'
        elem = self._make_soup_element(html)
        text, gid = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert "YOU MAY cache" in text
        assert gid == "python_design=html=opt-cache"

    def test_no_requirement_tag_returns_none_id(self):
        html = "<p>This is a plain paragraph with no requirement tag.</p>"
        elem = self._make_soup_element(html)
        text, gid = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert gid is None
        assert "plain paragraph" in text

    def test_note_include_replaced(self):
        html = '<p>{% include note.html content="This is important" %}</p>'
        elem = self._make_soup_element(html)
        text, _ = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert "**NOTE:** This is important" in text

    def test_important_include_replaced(self):
        html = '<p>{% include important.html content="Critical info" %}</p>'
        elem = self._make_soup_element(html)
        text, _ = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert "**IMPORTANT:** Critical info" in text

    def test_draft_include_replaced(self):
        html = '<p>{% include draft.html content="Draft content" %}</p>'
        elem = self._make_soup_element(html)
        text, _ = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert "**DRAFT:** Draft content" in text

    def test_icon_pattern_stripped(self):
        html = '<p>:heavy_check_mark: {% include requirement/MUST id="icon-test" %} do something.</p>'
        elem = self._make_soup_element(html)
        text, gid = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert ":heavy_check_mark:" not in text
        assert gid == "python_design=html=icon-test"


class TestIdExtraction:
    """Tests for guideline ID extraction from various file paths."""

    @staticmethod
    def _make_soup_element(html_text):
        from bs4 import BeautifulSoup

        soup = BeautifulSoup(html_text, "html.parser")
        return soup.find()

    def test_python_design_id(self):
        html = '<p>{% include requirement/MUST id="py-client-name" %} name clients well.</p>'
        elem = self._make_soup_element(html)
        _, gid = GuidelineIngestor._split_tags(elem, "docs/python/design.md")
        assert gid == "python_design=html=py-client-name"

    def test_java_implementation_id(self):
        html = '<p>{% include requirement/MUST id="java-impl-01" %} do things.</p>'
        elem = self._make_soup_element(html)
        _, gid = GuidelineIngestor._split_tags(elem, "docs/java/implementation.md")
        assert gid == "java_implementation=html=java-impl-01"

    def test_rust_introduction_id(self):
        html = '<p>{% include requirement/MUST id="rust-enums-non-exhaustive" %} not use non_exhaustive.</p>'
        elem = self._make_soup_element(html)
        _, gid = GuidelineIngestor._split_tags(elem, "docs/rust/introduction.md")
        assert gid == "rust_introduction=html=rust-enums-non-exhaustive"

    def test_dotnet_design_id(self):
        html = '<p>{% include requirement/SHOULD id="dotnet-naming" %} follow naming conventions.</p>'
        elem = self._make_soup_element(html)
        _, gid = GuidelineIngestor._split_tags(elem, "docs/dotnet/design.md")
        assert gid == "dotnet_design=html=dotnet-naming"

    def test_backslash_path_normalized(self):
        html = '<p>{% include requirement/MUST id="win-path" %} work on windows.</p>'
        elem = self._make_soup_element(html)
        _, gid = GuidelineIngestor._split_tags(elem, "docs\\python\\design.md")
        assert gid == "python_design=html=win-path"


class TestParseMarkdownFile:
    """Tests for full markdown file parsing."""

    def _make_ingestor(self):
        """Create an ingestor without touching external services."""
        # We only call static/instance methods that don't need __init__ resources
        ingestor = object.__new__(GuidelineIngestor)
        return ingestor

    def test_paragraph_guideline_extracted(self):
        ingestor = self._make_ingestor()
        md = '{% include requirement/MUST id="test-para" %} Do something important.\n'
        results = ingestor.parse_markdown_file("docs/python/design.md", md)
        assert len(results) == 1
        assert results[0].id == "python_design=html=test-para"
        assert "Do something important" in results[0].text
        assert results[0].language == "python"
        assert results[0].source_file_path == "docs/python/design.md"

    def test_list_item_guideline_extracted(self):
        ingestor = self._make_ingestor()
        md = (
            "- {% include requirement/MUST id=\"list-item-1\" %} First rule.\n"
            "- {% include requirement/SHOULD id=\"list-item-2\" %} Second rule.\n"
        )
        results = ingestor.parse_markdown_file("docs/java/design.md", md)
        assert len(results) == 2
        assert results[0].id == "java_design=html=list-item-1"
        assert results[1].id == "java_design=html=list-item-2"

    def test_code_block_appended_to_previous_guideline(self):
        ingestor = self._make_ingestor()
        md = (
            '{% include requirement/MUST id="code-test" %} Use the following pattern.\n\n'
            "```python\nprint('hello')\n```\n"
        )
        results = ingestor.parse_markdown_file("docs/python/design.md", md)
        assert len(results) == 1
        assert "print('hello')" in results[0].text

    def test_orphan_paragraph_appended_to_previous(self):
        ingestor = self._make_ingestor()
        md = (
            '{% include requirement/MUST id="orphan-test" %} Main rule.\n\n'
            "This is additional context that should be appended.\n"
        )
        results = ingestor.parse_markdown_file("docs/python/design.md", md)
        assert len(results) == 1
        assert "Main rule" in results[0].text
        assert "additional context" in results[0].text

    def test_language_detected_from_path(self):
        ingestor = self._make_ingestor()
        md = '{% include requirement/MUST id="lang-test" %} Rule.\n'

        for folder, expected_lang in [
            ("python", "python"),
            ("java", "java"),
            ("dotnet", "dotnet"),
            ("typescript", "typescript"),
            ("golang", "golang"),
            ("rust", "rust"),
            ("ios", "ios"),
            ("android", "android"),
        ]:
            results = ingestor.parse_markdown_file(f"docs/{folder}/design.md", md)
            assert results[0].language == expected_lang, f"Failed for {folder}"

    def test_general_folder_has_no_language(self):
        ingestor = self._make_ingestor()
        md = '{% include requirement/MUST id="general-test" %} Rule.\n'
        results = ingestor.parse_markdown_file("docs/general/design.md", md)
        assert results[0].language is None

    def test_multiple_guidelines_in_document(self):
        ingestor = self._make_ingestor()
        md = (
            '{% include requirement/MUST id="rule-1" %} First rule.\n\n'
            '{% include requirement/SHOULD id="rule-2" %} Second rule.\n\n'
            '{% include requirement/MAY id="rule-3" %} Third rule.\n'
        )
        results = ingestor.parse_markdown_file("docs/python/design.md", md)
        assert len(results) == 3
        ids = [r.id for r in results]
        assert "python_design=html=rule-1" in ids
        assert "python_design=html=rule-2" in ids
        assert "python_design=html=rule-3" in ids

    def test_links_added_to_text(self):
        ingestor = self._make_ingestor()
        md = '{% include requirement/MUST id="link-test" %} See [the docs](https://example.com) for details.\n'
        results = ingestor.parse_markdown_file("docs/python/design.md", md)
        assert len(results) == 1
        assert "https://example.com" in results[0].text

    def test_empty_content_returns_empty(self):
        ingestor = self._make_ingestor()
        results = ingestor.parse_markdown_file("docs/python/design.md", "")
        assert results == []

    def test_no_guidelines_in_plain_markdown(self):
        ingestor = self._make_ingestor()
        md = "# Title\n\nThis is just a regular paragraph.\n\n- A list item\n- Another item\n"
        results = ingestor.parse_markdown_file("docs/python/design.md", md)
        assert results == []


class TestConvertCodeTag:
    """Tests for HTML code tag to markdown conversion."""

    def test_code_tag_with_language(self):
        html = '<code class="language-python">print("hello")</code>'
        result = GuidelineIngestor._convert_code_tag_to_markdown(html)
        assert result == '```python\nprint("hello")\n```'

    def test_code_tag_with_different_language(self):
        html = '<code class="language-java">System.out.println("hello");</code>'
        result = GuidelineIngestor._convert_code_tag_to_markdown(html)
        assert result == '```java\nSystem.out.println("hello");\n```'

    def test_no_code_tag_returns_original(self):
        html = "<p>No code here</p>"
        result = GuidelineIngestor._convert_code_tag_to_markdown(html)
        assert result == html


# ============================================================================
# Content Hashing Tests
# ============================================================================


class TestContentHashing:
    """Tests for content hash computation and normalization."""

    def test_identical_content_same_hash(self):
        content = "DO use HTTPS for all API calls."
        hash1 = GuidelineIngestor.compute_content_hash(content)
        hash2 = GuidelineIngestor.compute_content_hash(content)
        assert hash1 == hash2

    def test_different_content_different_hash(self):
        hash1 = GuidelineIngestor.compute_content_hash("Rule A")
        hash2 = GuidelineIngestor.compute_content_hash("Rule B")
        assert hash1 != hash2

    def test_leading_trailing_whitespace_normalized(self):
        hash1 = GuidelineIngestor.compute_content_hash("content")
        hash2 = GuidelineIngestor.compute_content_hash("  content  ")
        hash3 = GuidelineIngestor.compute_content_hash("\n\ncontent\n\n")
        assert hash1 == hash2 == hash3

    def test_crlf_normalized_to_lf(self):
        hash_lf = GuidelineIngestor.compute_content_hash("line1\nline2")
        hash_crlf = GuidelineIngestor.compute_content_hash("line1\r\nline2")
        hash_cr = GuidelineIngestor.compute_content_hash("line1\rline2")
        assert hash_lf == hash_crlf == hash_cr

    def test_multiple_blank_lines_collapsed(self):
        hash1 = GuidelineIngestor.compute_content_hash("line1\n\nline2")
        hash2 = GuidelineIngestor.compute_content_hash("line1\n\n\n\nline2")
        hash3 = GuidelineIngestor.compute_content_hash("line1\n\n\n\n\n\n\nline2")
        assert hash1 == hash2 == hash3

    def test_single_blank_line_preserved(self):
        hash1 = GuidelineIngestor.compute_content_hash("line1\nline2")
        hash2 = GuidelineIngestor.compute_content_hash("line1\n\nline2")
        assert hash1 != hash2

    def test_hash_is_sha256_hex(self):
        result = GuidelineIngestor.compute_content_hash("test content")
        assert len(result) == 64
        assert all(c in "0123456789abcdef" for c in result)

    def test_hash_stability(self):
        """Hash should be deterministic across calls."""
        content = "DO provide a client that users can instantiate."
        expected = GuidelineIngestor.compute_content_hash(content)
        for _ in range(10):
            assert GuidelineIngestor.compute_content_hash(content) == expected


# ============================================================================
# ID Format Validation Tests
# ============================================================================


class TestIdFormatValidation:
    """Tests for Azure AI Search ID format compatibility checks."""

    def test_valid_ids_pass(self):
        items = [
            ParsedGuideline(id="python_design=html=py-client-name", text="t", language="python", source_file_path="f"),
            ParsedGuideline(id="java_impl=html=java-01", text="t", language="java", source_file_path="f"),
            ParsedGuideline(id="rust_introduction=html=rust-enums", text="t", language="rust", source_file_path="f"),
        ]
        malformed = GuidelineIngestor.check_id_format(items)
        assert malformed == []

    def test_empty_id_detected(self):
        items = [
            ParsedGuideline(id="", text="some text here for context", language="python", source_file_path="f"),
        ]
        malformed = GuidelineIngestor.check_id_format(items)
        assert len(malformed) == 1

    def test_none_id_detected(self):
        items = [
            ParsedGuideline(id=None, text="orphan text", language="python", source_file_path="f"),
        ]
        malformed = GuidelineIngestor.check_id_format(items)
        assert len(malformed) == 1

    def test_special_characters_rejected(self):
        items = [
            ParsedGuideline(id="bad id with spaces", text="t", language="python", source_file_path="f"),
        ]
        malformed = GuidelineIngestor.check_id_format(items)
        assert "bad id with spaces" in malformed

    def test_dot_in_id_rejected(self):
        items = [
            ParsedGuideline(id="python_design.html#foo", text="t", language="python", source_file_path="f"),
        ]
        malformed = GuidelineIngestor.check_id_format(items)
        assert "python_design.html#foo" in malformed

    def test_mixed_valid_and_invalid(self):
        items = [
            ParsedGuideline(id="valid_id=html=ok", text="t", language="python", source_file_path="f"),
            ParsedGuideline(id="has spaces", text="t", language="python", source_file_path="f"),
            ParsedGuideline(id="also_valid-123", text="t", language="python", source_file_path="f"),
        ]
        malformed = GuidelineIngestor.check_id_format(items)
        assert len(malformed) == 1
        assert "has spaces" in malformed

    def test_hyphens_and_underscores_allowed(self):
        items = [
            ParsedGuideline(id="a-b_c-d_e", text="t", language="python", source_file_path="f"),
        ]
        malformed = GuidelineIngestor.check_id_format(items)
        assert malformed == []

    def test_equals_sign_allowed(self):
        items = [
            ParsedGuideline(id="python_design=html=some-rule", text="t", language="python", source_file_path="f"),
        ]
        malformed = GuidelineIngestor.check_id_format(items)
        assert malformed == []


# ============================================================================
# Deduplication Tests
# ============================================================================


class TestDeduplication:
    """Tests for filter_duplicates logic."""

    def test_unique_items_all_good(self):
        items = [
            ParsedGuideline(id="id-1", text="Rule 1", language="python", source_file_path="f"),
            ParsedGuideline(id="id-2", text="Rule 2", language="python", source_file_path="f"),
            ParsedGuideline(id="id-3", text="Rule 3", language="python", source_file_path="f"),
        ]
        result = GuidelineIngestor.filter_duplicates(items)
        assert len(result["good"]) == 3
        assert len(result["bad"]) == 0

    def test_exact_copies_deduped_to_one(self):
        items = [
            ParsedGuideline(id="dup-id", text="Same text", language="python", source_file_path="f1"),
            ParsedGuideline(id="dup-id", text="Same text", language="python", source_file_path="f2"),
            ParsedGuideline(id="unique", text="Other", language="python", source_file_path="f"),
        ]
        result = GuidelineIngestor.filter_duplicates(items)
        assert len(result["good"]) == 2
        assert len(result["bad"]) == 0
        good_ids = [g.id for g in result["good"]]
        assert good_ids.count("dup-id") == 1

    def test_conflicting_duplicates_marked_bad(self):
        items = [
            ParsedGuideline(id="conflict-id", text="Version A", language="python", source_file_path="f1"),
            ParsedGuideline(id="conflict-id", text="Version B", language="python", source_file_path="f2"),
            ParsedGuideline(id="unique", text="Other", language="python", source_file_path="f"),
        ]
        result = GuidelineIngestor.filter_duplicates(items)
        assert len(result["good"]) == 1
        assert result["good"][0].id == "unique"
        assert len(result["bad"]) == 2
        assert all(b.id == "conflict-id" for b in result["bad"])

    def test_empty_list(self):
        result = GuidelineIngestor.filter_duplicates([])
        assert result["good"] == []
        assert result["bad"] == []

    def test_all_duplicates_same_content(self):
        items = [
            ParsedGuideline(id="same", text="Same", language="python", source_file_path="f1"),
            ParsedGuideline(id="same", text="Same", language="python", source_file_path="f2"),
            ParsedGuideline(id="same", text="Same", language="python", source_file_path="f3"),
        ]
        result = GuidelineIngestor.filter_duplicates(items)
        assert len(result["good"]) == 1
        assert len(result["bad"]) == 0

    def test_mixed_exact_and_conflicting_duplicates(self):
        items = [
            ParsedGuideline(id="exact-dup", text="Same text", language="python", source_file_path="f1"),
            ParsedGuideline(id="exact-dup", text="Same text", language="python", source_file_path="f2"),
            ParsedGuideline(id="conflict-dup", text="Version A", language="python", source_file_path="f1"),
            ParsedGuideline(id="conflict-dup", text="Version B", language="python", source_file_path="f2"),
            ParsedGuideline(id="unique", text="Only one", language="python", source_file_path="f"),
        ]
        result = GuidelineIngestor.filter_duplicates(items)
        good_ids = [g.id for g in result["good"]]
        assert "exact-dup" in good_ids
        assert good_ids.count("exact-dup") == 1
        assert "unique" in good_ids
        assert "conflict-dup" not in good_ids
        assert len(result["bad"]) == 2

    def test_order_preserved_for_good_items(self):
        items = [
            ParsedGuideline(id="c", text="C", language="python", source_file_path="f"),
            ParsedGuideline(id="a", text="A", language="python", source_file_path="f"),
            ParsedGuideline(id="b", text="B", language="python", source_file_path="f"),
        ]
        result = GuidelineIngestor.filter_duplicates(items)
        assert [g.id for g in result["good"]] == ["c", "a", "b"]
