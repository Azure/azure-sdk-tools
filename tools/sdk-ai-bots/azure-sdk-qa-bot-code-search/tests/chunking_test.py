from __future__ import annotations

import asyncio

from code_search.chunking import ParserFailureCollector
from code_search.chunking import chunk_source
from code_search.typespec_pool import TypeSpecWorkerPool


def test_chunking_detects_languages_and_automatically_uses_typespec_parser() -> None:
    pool = TypeSpecWorkerPool(size=1)
    failures = ParserFailureCollector()
    try:
        typescript_chunks = asyncio.run(
            chunk_source(
                "packages/example/main.ts",
                "export const value = 1;",
                typespec_pool=pool,
                parser_failures=failures,
            )
        )
        typespec_chunks = asyncio.run(
            chunk_source(
                "packages/example/main.tsp",
                "namespace Demo { model Example { value: string; } interface Api { read(): Example; } }",
                typespec_pool=pool,
                parser_failures=failures,
            )
        )
    finally:
        pool.close()

    assert typescript_chunks[0].language == "typescript"
    assert typescript_chunks[0].parser == "recursive"
    assert all(chunk.language == "typespec" for chunk in typespec_chunks)
    assert all(chunk.parser == "typespec-compiler" for chunk in typespec_chunks)
    assert {"Demo", "Example", "Api"} <= {
        chunk.symbol_name for chunk in typespec_chunks
    }
    assert typespec_chunks[0].path_prefixes == ("packages", "packages/example")
    assert not failures.snapshot()
