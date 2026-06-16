"""Critical-path tests for SourceAwareMarkItDownFileReader.

The reader stores the full ``#``-encoded input path as ``documents.title``
(globally unique across source folders) and the source folder in
``raw_data``.
"""

from __future__ import annotations

import datetime as _dt

import pytest

from src.graphrag.source_aware_reader import SourceAwareMarkItDownFileReader


class _FakeStorage:
    """Minimal async storage stub matching the bits read_file uses."""

    def __init__(self, content: bytes):
        self._content = content

    async def get(self, path: str, encoding: str = "utf-8", as_bytes: bool = False):
        return self._content

    async def get_creation_date(self, path: str):
        return _dt.datetime(2024, 1, 1, tzinfo=_dt.timezone.utc)


def _make_reader(content: bytes) -> SourceAwareMarkItDownFileReader:
    return SourceAwareMarkItDownFileReader(
        storage=_FakeStorage(content), file_pattern=".*"
    )


@pytest.mark.asyncio
async def test_title_is_full_encoded_path_with_source_folder():
    reader = _make_reader(b"# Some Heading\n\nbody text\n")
    docs = await reader.read_file("typespec_docs/sub#file.md")
    assert len(docs) == 1
    doc = docs[0]
    assert doc.title == "typespec_docs#sub#file.md"
    assert doc.raw_data == {
        "source_folder": "typespec_docs",
        "source_path": "typespec_docs/sub#file.md",
    }


@pytest.mark.asyncio
async def test_cross_folder_same_relative_path_titles_are_distinct():
    reader = _make_reader(b"# Readme\n")
    a = (await reader.read_file("typespec_docs/README.md"))[0]
    b = (await reader.read_file("python_docs/README.md"))[0]
    assert a.title == "typespec_docs#README.md"
    assert b.title == "python_docs#README.md"
