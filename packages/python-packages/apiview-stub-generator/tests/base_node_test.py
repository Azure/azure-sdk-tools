# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Tests for get_qualified_name() in _base_node.py.

These tests exercise the runtime-type-object path that astroid/AST parsing
bypasses. The FunctionNode/ClassNode tests go through source parsing and
therefore do NOT catch regressions in get_qualified_name when called with
actual Python type objects (which happens during class-variable introspection).
"""

import typing

import pytest

from apistub.nodes import get_qualified_name


class TestGetQualifiedNameOptionalUnion:
    """Ensure Union[X, None] collapses to Optional[X] regardless of Python version.

    Python 3.13 exposes typing.Union[int, None].__name__ == "Optional", so the
    Optional wrapper is applied correctly.  Python 3.14 changed the backing
    object so that __name__ == "Union"; without the fix get_qualified_name would
    produce the incorrect "Optional[Union[int]]".
    """

    def test_union_with_none_renders_as_optional(self):
        """typing.Union[int, None] must render as Optional[int], not Optional[Union[int]]."""
        result = get_qualified_name(typing.Union[int, None], "test")
        assert result == "Optional[int]", (
            f"Expected 'Optional[int]' but got {result!r}. "
            "On Python 3.14, Union[int, None].__name__ is 'Union' instead of "
            "'Optional'; get_qualified_name must still collapse this to Optional[int]."
        )

    def test_union_with_none_and_multiple_types(self):
        """typing.Union[int, str, None] must render as Optional[Union[int, str]]."""
        result = get_qualified_name(typing.Union[int, str, None], "test")
        assert result == "Optional[Union[int, str]]", (
            f"Expected 'Optional[Union[int, str]]' but got {result!r}."
        )

    def test_optional_str_renders_correctly(self):
        """typing.Optional[str] (sugar for Union[str, None]) must render as Optional[str]."""
        result = get_qualified_name(typing.Optional[str], "test")
        assert result == "Optional[str]", (
            f"Expected 'Optional[str]' but got {result!r}."
        )

    def test_union_without_none_is_unchanged(self):
        """typing.Union[int, str] (no None) must render as Union[int, str]."""
        result = get_qualified_name(typing.Union[int, str], "test")
        assert result == "Union[int, str]", (
            f"Expected 'Union[int, str]' but got {result!r}."
        )

    def test_pep604_int_or_none_renders_as_optional(self):
        """int | None (PEP 604 syntax) must render as Optional[int]."""
        obj = int | None
        result = get_qualified_name(obj, "test")
        assert result == "Optional[int]", (
            f"Expected 'Optional[int]' for 'int | None' but got {result!r}."
        )

    def test_pep604_multi_type_or_none_renders_as_optional_union(self):
        """int | str | None (PEP 604) must render as Optional[Union[int, str]]."""
        obj = int | str | None
        result = get_qualified_name(obj, "test")
        assert result == "Optional[Union[int, str]]", (
            f"Expected 'Optional[Union[int, str]]' for 'int | str | None' but got {result!r}."
        )
