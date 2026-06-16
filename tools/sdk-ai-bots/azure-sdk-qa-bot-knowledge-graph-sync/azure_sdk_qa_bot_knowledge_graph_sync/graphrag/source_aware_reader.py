"""Source-aware MarkItDown input reader for GraphRAG.

A thin ``MarkItDownFileReader`` subclass that preserves source provenance
when GraphRAG ingests markdown from the knowledge container:

* Stores the originating ``source_folder`` (and full ``source_path``) in
  ``TextDocument.raw_data``, which GraphRAG propagates into the
  ``raw_data`` column of ``documents.parquet`` (see
  ``graphrag.data_model.schemas.DOCUMENTS_FINAL_COLUMNS``). The bot reads
  ``raw_data["source_folder"]`` to attribute each graph reference back to
  its ``KnowledgeSource``.
* Sets ``documents.title`` to the full ``#``-encoded input path so titles
  are globally unique across source folders and round-trip back to the
  document link.

Register it with :func:`register_source_aware_input_reader` before
calling ``build_index`` (see ``run_indexing.py``).
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
    """``MarkItDownFileReader`` variant that preserves source provenance.

    Populates ``TextDocument.raw_data`` with the source folder and full
    input path; otherwise mirrors the upstream reader (including its
    title behaviour).

    Output ``raw_data`` schema:

    * ``source_folder`` — first path segment of the input file
      (``"typespec_docs"`` for ``typespec_docs/foo.md``). Empty string
      for a container-root blob.
    * ``source_path`` — full input path (``typespec_docs/sub#file.md``).
      The bot resolves each graph reference's link directly from this
      (drop the ``source_folder`` prefix, ``#``-encode the rest) so link
      resolution does not depend on ``documents.title``.

    ``documents.title`` is left as the upstream reader produces it
    (MarkItDown-extracted heading, falling back to the basename); it is
    not consumed by the bot.
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

        document = TextDocument(
            id=gen_sha512_hash({"text": text}, ["text"]),
            title=result.title if result.title else str(posix_path.name),
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
