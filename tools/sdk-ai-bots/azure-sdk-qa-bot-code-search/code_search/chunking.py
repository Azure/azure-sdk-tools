from __future__ import annotations

import hashlib
import logging
import re
import threading
from pathlib import PurePosixPath

import cocoindex as coco
from cocoindex.connectors import localfs
from cocoindex.ops.text import RecursiveSplitter, detect_code_language

from .models import ParserFailure, PreparedChunk
from .typespec_pool import TypeSpecParseError, TypeSpecWorkerPool

logger = logging.getLogger(__name__)

_IDENTIFIER = re.compile(r"\b[A-Za-z_][A-Za-z0-9_]{2,}\b")
_DEFAULT_SPLITTER = RecursiveSplitter()
TYPESPEC_POOL = coco.ContextKey[TypeSpecWorkerPool]("azure-sdk-code-typespec-pool")
PARSER_FAILURES = coco.ContextKey["ParserFailureCollector"](
    "azure-sdk-code-parser-failures"
)


class ParserFailureCollector:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._items: list[ParserFailure] = []
        self._seen: set[tuple[str, str]] = set()

    def add(self, path: str, message: str) -> None:
        with self._lock:
            if (path, message) in self._seen:
                return
            self._seen.add((path, message))
            self._items.append(ParserFailure(path, message))

    def snapshot(self) -> tuple[ParserFailure, ...]:
        with self._lock:
            return tuple(self._items)


@coco.fn(memo=True, version=2)
async def prepare_file(file: localfs.File) -> list[PreparedChunk]:
    path = file.file_path.path.as_posix()
    content_bytes = await file.read()
    try:
        content = content_bytes.decode("utf-8")
    except UnicodeDecodeError:
        logger.warning("skipping non-UTF-8 file %s", path)
        return []
    return await chunk_source(
        path,
        content,
        typespec_pool=coco.use_context(TYPESPEC_POOL),
        parser_failures=coco.use_context(PARSER_FAILURES),
    )


async def chunk_source(
    path: str,
    content: str,
    *,
    typespec_pool: TypeSpecWorkerPool,
    parser_failures: ParserFailureCollector,
) -> list[PreparedChunk]:
    extension = PurePosixPath(path).suffix.lower()
    if extension == ".tsp":
        try:
            declarations = await typespec_pool.parse(content)
        except TypeSpecParseError as exc:
            message = str(exc)
            parser_failures.add(path, message)
            logger.warning(
                "TypeSpec parser failed for %s; falling back to RecursiveSplitter: %s",
                path,
                message,
            )
            return _prepared(
                path,
                "typespec",
                "recursive-fallback",
                _split(content, "typespec"),
                parser_failure=message,
            )

        raw_chunks: list[tuple[str, int, int, int, int, str | None, str | None]] = []
        for declaration in declarations:
            declaration_text = content[declaration.start : declaration.end]
            if not declaration_text.strip():
                continue
            if len(declaration_text.encode("utf-8")) <= 1000:
                raw_chunks.append(
                    (
                        declaration_text,
                        declaration.start,
                        declaration.end,
                        declaration.start_line,
                        declaration.end_line,
                        declaration.name,
                        declaration.kind,
                    )
                )
                continue
            for chunk in _DEFAULT_SPLITTER.split(
                declaration_text,
                1000,
                min_chunk_size=250,
                chunk_overlap=150,
                language="typespec",
            ):
                raw_chunks.append(
                    (
                        chunk.text,
                        declaration.start + chunk.start.char_offset,
                        declaration.start + chunk.end.char_offset,
                        declaration.start_line + chunk.start.line - 1,
                        declaration.start_line + chunk.end.line - 1,
                        declaration.name,
                        declaration.kind,
                    )
                )
        if raw_chunks:
            return _prepared(path, "typespec", "typespec-compiler", raw_chunks)

    language = detect_code_language(filename=path) or extension.lstrip(".") or "text"
    return _prepared(path, language, "recursive", _split(content, language))


def _split(
    content: str, language: str
) -> list[tuple[str, int, int, int, int, str | None, str | None]]:
    chunks = _DEFAULT_SPLITTER.split(
        content,
        1000,
        min_chunk_size=250,
        chunk_overlap=150,
        language=language,
    )
    return [
        (
            chunk.text,
            chunk.start.char_offset,
            chunk.end.char_offset,
            chunk.start.line,
            chunk.end.line,
            None,
            None,
        )
        for chunk in chunks
        if chunk.text.strip()
    ]


def _prepared(
    path: str,
    language: str,
    parser: str,
    chunks: list[tuple[str, int, int, int, int, str | None, str | None]],
    parser_failure: str | None = None,
) -> list[PreparedChunk]:
    matched_prefixes = _path_prefixes(path)
    artifact_type = _artifact_type(path)
    output: list[PreparedChunk] = []
    for ordinal, (text, _start, _end, start_line, end_line, symbol, kind) in enumerate(
        chunks
    ):
        content_hash = hashlib.sha256(text.encode("utf-8")).hexdigest()
        logical_id = (
            f"{path}:{parser}:{kind or 'chunk'}:{symbol or ordinal}:"
            f"{start_line}:{end_line}"
        )
        output.append(
            PreparedChunk(
                logical_id=logical_id,
                path=path,
                path_prefixes=matched_prefixes,
                language=language,
                artifact_type=artifact_type,
                content=text,
                content_hash=content_hash,
                start_line=start_line,
                end_line=end_line,
                symbol_name=symbol,
                symbol_kind=kind,
                identifiers=tuple(dict.fromkeys(_IDENTIFIER.findall(text)))[:128],
                parser=parser,
                parser_failure=parser_failure,
            )
        )
    return output


def _path_prefixes(path: str) -> tuple[str, ...]:
    parts = PurePosixPath(path).parts[:-1]
    return tuple(PurePosixPath(*parts[:index]).as_posix() for index in range(1, len(parts) + 1))


def _artifact_type(path: str) -> str:
    parts = {part.lower() for part in PurePosixPath(path).parts}
    suffix = PurePosixPath(path).suffix.lower()
    if parts & {"test", "tests", "spec", "specs"}:
        return "test"
    if parts & {"sample", "samples", "example", "examples"}:
        return "sample"
    if parts & {"generated", "gen"}:
        return "generated"
    if suffix in {
        ".json",
        ".yaml",
        ".yml",
        ".toml",
        ".xml",
        ".props",
        ".targets",
        ".config",
    }:
        return "configuration"
    return "source"
