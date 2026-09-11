from __future__ import annotations

import argparse
import asyncio
import logging

from azure.identity import DefaultAzureCredential

from .builder import build_all
from .config import builder_settings, load
from .garbage_collection import collect_garbage
from .index_schema import ensure_index


def main() -> None:
    parser = argparse.ArgumentParser(description="Build the Azure SDK code index.")
    parser.add_argument(
        "command", choices=("ensure-index", "build-all", "collect-garbage")
    )
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args()
    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    logging.getLogger(
        "azure.core.pipeline.policies.http_logging_policy"
    ).setLevel(logging.WARNING)
    logging.getLogger("httpx").setLevel(logging.WARNING)

    credential = DefaultAzureCredential(process_timeout=60)
    try:
        load(credential)
        settings = builder_settings()
        if args.command == "ensure-index":
            ensure_index(settings, credential)
        elif args.command == "collect-garbage":
            collect_garbage(settings, credential)
        else:
            asyncio.run(build_all(settings, credential))
    finally:
        credential.close()


if __name__ == "__main__":
    main()
