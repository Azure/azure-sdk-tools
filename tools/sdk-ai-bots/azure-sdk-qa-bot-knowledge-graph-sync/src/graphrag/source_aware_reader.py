"""Source-aware MarkItDown input reader for GraphRAG.

The default ``MarkItDownFileReader`` from ``graphrag_input`` writes
``TextDocument.raw_data = None`` and stores only ``Path(path).name`` as
the document title — both the parent folder and the full input path
are dropped before the row hits ``documents.parquet``.

The knowledge-sync project uploads every markdown under
``{source_folder}/{filename}`` where ``source_folder`` matches the
``KnowledgeSource.name`` used by the bot's KB tool (e.g.
``typespec_docs``, ``azure_sdk_for_python_docs``). Losing that prefix
forces every downstream consumer (bot agent, debugging tooling) to
re-derive the source folder by listing the original input container —
which is both expensive at startup and **wrong** when multiple
folders contain the same basename (``README.md`` appears under at
least four SDK folders today).

This module solves the problem at the source: a thin subclass of
``MarkItDownFileReader`` parses the leading path segment and stores it
in ``TextDocument.raw_data``, which GraphRAG's
``load_input_documents`` workflow writes through as the ``raw_data``
column of ``documents.parquet`` (see
``graphrag.data_model.schemas.DOCUMENTS_FINAL_COLUMNS``). The bot
agent then reads ``raw_data["source_folder"]`` directly per document
— no blob listing, no basename collisions.

Use :func:`register_source_aware_input_reader` to wire the override
into GraphRAG's factory before calling ``build_index`` (see
``run_indexing.py``).
"""

from __future__ import annotations

import logging
from io import BytesIO
from pathlib import PurePosixPath

from graphrag_input.hashing import gen_sha512_hash
from graphrag_input.input_reader_factory import register_input_reader
from graphrag_input.input_type import InputType
from graphrag_input.markitdown import MarkItDownFileReader
from graphrag_input.text_document import TextDocument
from markitdown import MarkItDown, StreamInfo

logger = logging.getLogger(__name__)


class SourceAwareMarkItDownFileReader(MarkItDownFileReader):
    """``MarkItDownFileReader`` variant that preserves the source folder.

    Behaviour mirrors the upstream reader exactly except that
    ``TextDocument.raw_data`` is populated with the parent path
    segments parsed out of the input path. GraphRAG's input pipeline
    propagates ``raw_data`` verbatim into ``documents.parquet``.

    Output ``raw_data`` schema:

    * ``source_folder`` — first path segment of the input file
      (``"typespec_docs"`` for ``typespec_docs/foo.md``). Empty string
      when the input was uploaded at the container root with no folder.
    * ``source_path`` — full input path as provided by graphrag's
      storage layer, kept for debuggability.

    Document ``title`` is set to the **full input path encoded with**
    ``#`` (``typespec_docs/sub#file.md`` → ``typespec_docs#sub#file.md``)
    so that it is:

    * **Globally unique & stable** across runs — GraphRAG's incremental
      update keys its document delta on ``documents.title``
      (``graphrag.index.update.incremental_index.get_delta_docs``), so a
      title that is unique per source document (and identical for the
      same document across runs) is required for correct add/delete
      detection. The previous behaviour stored ``result.title`` (the
      MarkItDown-extracted heading, ``None`` for plain markdown today) or
      fell back to the bare ``posix_path.name`` — which collides across
      source folders (every folder's ``README.md`` shares one title) and
      would silently break delta detection.
    * **Round-trippable to the original path** — the bot agent reverses
      the ``#`` encoding (and strips the ``source_folder`` prefix it
      reads from ``raw_data``) to resolve the document link, mirroring
      the KB tool's ``title.replace("#", "/")`` contract.

    We deliberately ignore ``result.title``: the knowledge container only
    holds ``.md`` (for which MarkItDown returns no title), and relying on
    it is fragile — a MarkItDown upgrade that started extracting an H1
    would replace the path key with arbitrary heading text and break both
    delta detection and link resolution.
    """

    async def read_file(self, path: str) -> list[TextDocument]:
        bytes_ = await self._storage.get(
            path, encoding=self._encoding, as_bytes=True
        )
        md = MarkItDown()
        # Use PurePosixPath: graphrag's storage layer always exposes
        # blob paths with '/' separators regardless of host OS, and the
        # suffix lookup must match the original extension casing.
        posix_path = PurePosixPath(path)
        result = md.convert_stream(
            BytesIO(bytes_),
            stream_info=StreamInfo(extension=posix_path.suffix),
        )
        text = result.markdown

        source_folder = posix_path.parts[0] if len(posix_path.parts) > 1 else ""

        # Encode the full input path as the document title (globally
        # unique, stable, round-trippable). See the class docstring for
        # why we do not use ``result.title`` / the bare filename.
        encoded_title = str(posix_path).replace("/", "#")

        document = TextDocument(
            id=gen_sha512_hash({"text": text}, ["text"]),
            title=encoded_title,
            text=text,
            creation_date=await self._storage.get_creation_date(path),
            raw_data={
                "source_folder": source_folder,
                "source_path": path,
            },
        )
        return [document]


def register_source_aware_input_reader() -> None:
    """Override GraphRAG's default ``markitdown`` reader with ours.

    GraphRAG's ``create_input_reader`` only registers its built-in
    reader when the strategy is *not* already in the factory; calling
    this function before ``build_index`` ensures our override wins.
    Calling it more than once is safe — ``Factory.register`` performs
    a plain dict assignment.
    """
    register_input_reader(
        InputType.MarkItDown, SourceAwareMarkItDownFileReader
    )
    logger.info(
        "Registered SourceAwareMarkItDownFileReader for input type %r; "
        "documents.parquet rows will carry raw_data['source_folder']",
        InputType.MarkItDown,
    )
