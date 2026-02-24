# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""
Tests for resolve_language and resolve_language_to_canonical in cli.py.
"""

import pytest
from cli import _LANGUAGE_ALIAS_TABLE, resolve_language, resolve_language_to_canonical


@pytest.fixture(autouse=True)
def _clear_alias_table():
    """Clear the cached alias table before each test for isolation."""
    _LANGUAGE_ALIAS_TABLE.clear()
    yield
    _LANGUAGE_ALIAS_TABLE.clear()


# ---------- Canonical names resolve correctly ----------


@pytest.mark.parametrize(
    "canonical, expected_pretty",
    [
        ("android", "Android"),
        ("clang", "Clang"),
        ("cpp", "C++"),
        ("dotnet", "C#"),
        ("golang", "Go"),
        ("ios", "Swift"),
        ("java", "Java"),
        ("python", "Python"),
        ("rust", "Rust"),
        ("typescript", "JavaScript"),
    ],
)
def test_canonical_names(canonical, expected_pretty):
    assert resolve_language(canonical) == (canonical, expected_pretty)


# ---------- Pretty names resolve correctly ----------


@pytest.mark.parametrize(
    "pretty, expected_canonical",
    [
        ("Android", "android"),
        ("Clang", "clang"),
        ("C++", "cpp"),
        ("C#", "dotnet"),
        ("Go", "golang"),
        ("Swift", "ios"),
        ("Java", "java"),
        ("Python", "python"),
        ("Rust", "rust"),
        ("JavaScript", "typescript"),
    ],
)
def test_pretty_names(pretty, expected_canonical):
    canonical, returned_pretty = resolve_language(pretty)
    assert canonical == expected_canonical
    assert returned_pretty == pretty


# ---------- Aliases resolve correctly ----------


@pytest.mark.parametrize(
    "alias, expected",
    [
        ("csharp", ("dotnet", "C#")),
        ("C#", ("dotnet", "C#")),
        ("c#", ("dotnet", "C#")),
        ("c++", ("cpp", "C++")),
        ("go", ("golang", "Go")),
        ("swift", ("ios", "Swift")),
        ("c", ("clang", "Clang")),
        ("javascript", ("typescript", "JavaScript")),
    ],
)
def test_aliases(alias, expected):
    assert resolve_language(alias) == expected


# ---------- Case-insensitivity ----------


@pytest.mark.parametrize(
    "variant, expected",
    [
        ("PYTHON", ("python", "Python")),
        ("Python", ("python", "Python")),
        ("python", ("python", "Python")),
        ("GOLANG", ("golang", "Go")),
        ("GoLang", ("golang", "Go")),
        ("CSHARP", ("dotnet", "C#")),
        ("Csharp", ("dotnet", "C#")),
        ("DOTNET", ("dotnet", "C#")),
        ("DotNet", ("dotnet", "C#")),
        ("JAVA", ("java", "Java")),
    ],
)
def test_case_insensitivity(variant, expected):
    assert resolve_language(variant) == expected


# ---------- Invalid languages raise ValueError ----------


def test_invalid_language():
    with pytest.raises(ValueError, match="Unsupported language"):
        resolve_language("foobar")


def test_invalid_language_message_contains_name():
    with pytest.raises(ValueError, match="foobar"):
        resolve_language("foobar")


# ---------- Empty string raises ValueError ----------


def test_empty_string():
    with pytest.raises(ValueError, match="must not be empty"):
        resolve_language("")


# ---------- resolve_language_to_canonical ----------


def test_resolve_language_to_canonical_returns_canonical():
    assert resolve_language_to_canonical("Go") == "golang"


def test_resolve_language_to_canonical_from_alias():
    assert resolve_language_to_canonical("csharp") == "dotnet"


def test_resolve_language_to_canonical_invalid():
    with pytest.raises(ValueError):
        resolve_language_to_canonical("notalanguage")
