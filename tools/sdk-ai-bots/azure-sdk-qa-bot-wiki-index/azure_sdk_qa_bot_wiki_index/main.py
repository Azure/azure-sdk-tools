"""CLI — build the per-document wiki layer into the shared KB index (push path).

Phase 0 state: per-document LLM synthesis → ``page_type="summary"`` pages pushed
into ``azure-sdk-knowledge``. The cross-document wiki pages (entity/concept/
synthesis) and the storage/indexer path are added in later phases.

Usage::

    python -m azure_sdk_qa_bot_wiki_index.main --prefix typespec_docs/
    python -m azure_sdk_qa_bot_wiki_index.main --purge          # remove all generated docs
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import os

from azure.identity.aio import DefaultAzureCredential as AsyncDefaultAzureCredential
from azure.search.documents.aio import SearchClient as AzureSearchClient
from azure.storage.blob.aio import BlobServiceClient

from .documents import WikiDoc, delete_generated_docs, push_docs
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
                removed = await delete_generated_docs(search_client)
                logger.info("purged %d generated docs", removed)
                if args.no_generate:
                    return 0

            corpus = await _read_corpus(credential, prefixes, args.limit)
            if not corpus:
                logger.warning("no markdown found under prefixes %r", prefixes)
                return 0

            aoai = build_azure_openai_client(_env("AZURE_OPENAI_ENDPOINT"))
            llm = ChatLLM(aoai, _env("WIKI_SYNTHESIS_DEPLOYMENT", "gpt-5.4"))
            embedder = Embedder(aoai, _env("WIKI_EMBEDDING_DEPLOYMENT", "text-embedding-ada-002"))

            docs: list[WikiDoc] = build_wiki_pages(corpus, llm)
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
            logger.info("done: %d/%d docs pushed", pushed, len(docs))
    return 0


def main() -> None:
    parser = argparse.ArgumentParser(description="Build the wiki layer into Azure AI Search.")
    parser.add_argument("--prefix", default="", help="comma-separated blob name prefixes (e.g. typespec_docs/,typespec_azure_docs/)")
    parser.add_argument("--limit", type=int, default=0, help="cap number of source docs (0 = all)")
    parser.add_argument("--purge", action="store_true", help="delete all generated docs first")
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
