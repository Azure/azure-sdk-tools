"""Critical-path tests for SourceAwareMarkItDownFileReader.

The reader records the source folder and full input path in
``raw_data`` (the bot resolves graph-reference links from these). The
``title`` is left as the upstream reader produces it and is not consumed
by the bot.
"""

from __future__ import annotations

import datetime as _dt

import pytest

from azure_sdk_qa_bot_knowledge_graph_sync.graphrag.source_aware_reader import (
    SourceAwareMarkItDownFileReader,
)


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
async def test_raw_data_carries_source_folder_and_path():
    reader = _make_reader(b"# Some Heading\n\nbody text\n")
    docs = await reader.read_file("typespec_docs/sub#file.md")
    assert len(docs) == 1
    assert docs[0].raw_data == {
        "source_folder": "typespec_docs",
        "source_path": "typespec_docs/sub#file.md",
    }
