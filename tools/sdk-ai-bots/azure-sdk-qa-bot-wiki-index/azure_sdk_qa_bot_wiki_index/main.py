"""CLI — build the wiki and/or graph layers into the shared KB index.

The two layers are **independent** creation pipelines (mirroring WeKnora's
separate ``WikiEnabled`` / ``GraphEnabled`` toggles):

* ``wiki``  — per-document LLM synthesis → ``page_type="wiki"`` pages.
* ``graph`` — LLM entity + relationship extraction, PMI/strength weights, and
  1-hop/2-hop edges → ``page_type="entity"`` / ``"relationship"`` pages.

Usage::

    # wiki only
    python -m azure_sdk_qa_bot_wiki_index.main --build wiki  --prefix typespec_docs/
    # graph only
    python -m azure_sdk_qa_bot_wiki_index.main --build graph --prefix typespec_docs/
    # both
    python -m azure_sdk_qa_bot_wiki_index.main --build all
    # purge just the graph layer (leaves wiki intact)
    python -m azure_sdk_qa_bot_wiki_index.main --build graph --purge --no-generate

Config (environment variables)::

    AI_SEARCH_BASE_URL             Azure AI Search endpoint
    AI_SEARCH_INDEX                target index (shared with the KB)
    STORAGE_BLOB_ENDPOINT          blob account endpoint
    STORAGE_KNOWLEDGE_CONTAINER    knowledge container name (default: knowledge)
    AZURE_OPENAI_ENDPOINT          Azure OpenAI endpoint
    WIKI_SYNTHESIS_DEPLOYMENT      chat deployment (default: gpt-5.4)
    WIKI_EMBEDDING_DEPLOYMENT      embedding deployment (default: text-embedding-ada-002)
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import os

from azure.identity.aio import DefaultAzureCredential as AsyncDefaultAzureCredential
from azure.search.documents.aio import SearchClient as AzureSearchClient
from azure.storage.blob.aio import BlobServiceClient

from .documents import (
    GRAPH_PAGE_TYPES,
    WIKI_PAGE_TYPES,
    WikiDoc,
    delete_docs_by_page_types,
    push_docs,
)
from .graph import build_graph
from .llm import ChatLLM, Embedder, build_azure_openai_client
from .reader import read_blob_container
from .wiki import build_wiki_pages, embed_docs

logger = logging.getLogger(__name__)


def _env(name: str, default: str = "") -> str:
    return os.environ.get(name, default)


def _make_search_client(credential) -> AzureSearchClient:
    return AzureSearchClient(
        endpoint=_env("AI_SEARCH_BASE_URL").rstrip("/"),
        index_name=_env("AI_SEARCH_INDEX"),
        credential=credential,
    )


def _make_blob_service_client(credential) -> BlobServiceClient:
    return BlobServiceClient(account_url=_env("STORAGE_BLOB_ENDPOINT"), credential=credential)


def _page_types_for(build: str) -> tuple[str, ...]:
    if build == "wiki":
        return WIKI_PAGE_TYPES
    if build == "graph":
        return GRAPH_PAGE_TYPES
    return tuple(sorted(set(WIKI_PAGE_TYPES + GRAPH_PAGE_TYPES)))


async def _read_corpus(credential, prefixes: list[str], limit: int) -> list[tuple[str, str]]:
    container = _env("STORAGE_KNOWLEDGE_CONTAINER", "knowledge")
    blob_service = _make_blob_service_client(credential)
    corpus: list[tuple[str, str]] = []
    async with blob_service:
        cc = blob_service.get_container_client(container)
        for pfx in prefixes:
            corpus += await read_blob_container(cc, prefix=pfx)
    if limit and limit > 0:
        corpus = corpus[:limit]
        logger.info("limited corpus to first %d docs", len(corpus))
    return corpus


async def _run(args: argparse.Namespace) -> int:
    prefixes = [p.strip() for p in args.prefix.split(",") if p.strip()] or [""]
    async with AsyncDefaultAzureCredential() as credential:
        search_client = _make_search_client(credential)
        async with search_client:
            if args.purge:
                removed = await delete_docs_by_page_types(
                    search_client, _page_types_for(args.build)
                )
                logger.info("purged %d %s docs", removed, args.build)
            if args.no_generate:
                return 0

            corpus = await _read_corpus(credential, prefixes, args.limit)
            if not corpus:
                logger.warning("no markdown found under prefixes %r", prefixes)
                return 0

            aoai = build_azure_openai_client(_env("AZURE_OPENAI_ENDPOINT"))
            llm = ChatLLM(aoai, _env("WIKI_SYNTHESIS_DEPLOYMENT", "gpt-5.4"))
            embedder = Embedder(aoai, _env("WIKI_EMBEDDING_DEPLOYMENT", "text-embedding-ada-002"))

            docs: list[WikiDoc] = []
            if args.build in ("wiki", "all"):
                docs += build_wiki_pages(corpus, llm)
            if args.build in ("graph", "all"):
                docs += build_graph(corpus, llm)

            if not docs:
                logger.warning("no docs generated")
                return 0

            embed_docs(docs, embedder)

            if args.dry_run:
                logger.info("dry-run: generated %d docs, not pushing", len(docs))
                for d in docs[:5]:
                    logger.info("sample [%s] %s\n%s", d.page_type, d.header_1, d.chunk[:400])
                return 0

            pushed = await push_docs(search_client, docs)
            logger.info("done: %d/%d docs pushed (%s)", pushed, len(docs), args.build)
    return 0


def main() -> None:
    parser = argparse.ArgumentParser(description="Build wiki/graph layers into Azure AI Search.")
    parser.add_argument(
        "--build",
        choices=["wiki", "graph", "all"],
        default="wiki",
        help="which independent pipeline to run",
    )
    parser.add_argument("--prefix", default="", help="comma-separated blob name prefixes (e.g. typespec_docs/,typespec_azure_docs/)")
    parser.add_argument("--limit", type=int, default=0, help="cap number of source docs (0 = all)")
    parser.add_argument("--purge", action="store_true", help="delete existing docs of the selected layer(s) first")
    parser.add_argument("--no-generate", action="store_true", help="with --purge: only purge, do not rebuild")
    parser.add_argument("--dry-run", action="store_true", help="generate + embed but do not push")
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    raise SystemExit(asyncio.run(_run(args)))


if __name__ == "__main__":
    main()
