"""CLI — WeKnora-faithful wiki MapReduce build.

Phase 1 state: generates the wiki page set (summary + entity/concept/index) as
in-memory :class:`WikiPage` objects. ``--dry-run`` inspects them; blob
persistence + the dedicated indexer are added in later phases. ``--purge``
removes previously-pushed generated docs from the index.

Usage::

    python -m azure_sdk_qa_bot_wiki_index.main --prefix typespec_docs/ --dry-run
    python -m azure_sdk_qa_bot_wiki_index.main --purge --no-generate
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import os
from collections import Counter

from azure.identity.aio import DefaultAzureCredential as AsyncDefaultAzureCredential
from azure.search.documents.aio import SearchClient as AzureSearchClient
from azure.storage.blob.aio import BlobServiceClient

from .documents import delete_generated_docs
from .llm import ChatLLM, build_azure_openai_client
from .reader import read_blob_container
from .wiki import build_wiki

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
        if args.purge:
            search_client = _make_search_client(credential)
            async with search_client:
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

        pages = build_wiki(corpus, llm, min_docs=args.min_docs)
        if not pages:
            logger.warning("no wiki pages generated")
            return 0

        counts = Counter(p.page_type for p in pages)
        logger.info("generated pages by type: %s", dict(counts))

        if args.dry_run:
            for pt in ("summary", "entity", "concept", "index"):
                sample = next((p for p in pages if p.page_type == pt), None)
                if sample:
                    logger.info(
                        "sample [%s] slug=%s title=%s links=%s\n%s",
                        pt, sample.slug, sample.title, sample.out_links[:4],
                        sample.content[:400],
                    )
            return 0

        logger.warning("blob persistence not implemented yet (Phase 2); use --dry-run")
    return 0


def main() -> None:
    parser = argparse.ArgumentParser(description="Build the WeKnora-style wiki page set.")
    parser.add_argument("--prefix", default="", help="comma-separated blob name prefixes")
    parser.add_argument("--limit", type=int, default=0, help="cap number of source docs (0 = all)")
    parser.add_argument("--min-docs", type=int, default=2, help="min source docs for an entity/concept page")
    parser.add_argument("--purge", action="store_true", help="delete all generated docs from the index first")
    parser.add_argument("--no-generate", action="store_true", help="with --purge: only purge")
    parser.add_argument("--dry-run", action="store_true", help="build + inspect, do not persist")
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    raise SystemExit(asyncio.run(_run(args)))


if __name__ == "__main__":
    main()
