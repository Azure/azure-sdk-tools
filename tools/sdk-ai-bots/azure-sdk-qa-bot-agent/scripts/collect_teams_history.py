"""Export Teams channel threads to JSON; Cosmos writes require --save-to-cosmos."""

from __future__ import annotations

import argparse
import asyncio
import json
import logging
import sys
from pathlib import Path

from dotenv import load_dotenv
from azure.identity.aio import AzureCliCredential

_PROJECT_DIR = Path(__file__).resolve().parent.parent
load_dotenv(_PROJECT_DIR / ".env", override=False)
if str(_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(_PROJECT_DIR))

from config import app_config
from services.teams_collection_service import (
    TeamsHistoryRequest,
    collect_channel_history,
    validate_history_request,
)
from utils.azure_cosmosdb import close_cosmos_client
from utils.azure_credential import close_credential


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--team-id", required=True, help="Teams team GUID.")
    parser.add_argument("--channel-id", required=True)
    parser.add_argument("--output", required=True, type=Path, help="New JSON file; existing files are not overwritten.")
    parser.add_argument("--logic-app-url", help="SAS-free collection callback URL; otherwise read from App Configuration.")
    parser.add_argument("--logic-app-scope", help="Entra delegated scope for the selected environment's invocation role.")
    parser.add_argument("--start-time", help="Inclusive root-post creation cutoff (ISO 8601 with timezone).")
    parser.add_argument("--max-pages", type=int, help="Maximum root-post pages, 1-1000 (default: 1000).")
    parser.add_argument("--save-to-cosmos", action="store_true", help="Also create missing Cosmos messages (default: off).")
    parser.add_argument("--tenant-id", help="Bot tenant ID; required only with --save-to-cosmos.")
    args = parser.parse_args(argv)
    if args.save_to_cosmos and not args.tenant_id:
        parser.error("--tenant-id is required with --save-to-cosmos")
    return args


async def run(args: argparse.Namespace) -> dict[str, int]:
    request = TeamsHistoryRequest(
        team_id=args.team_id, channel_id=args.channel_id, tenant_id=args.tenant_id,
        start_time=args.start_time, max_pages=args.max_pages, save_to_cosmos=args.save_to_cosmos,
    )
    validate_history_request(request)
    if args.output.exists():
        raise FileExistsError(f"Output file already exists: {args.output}")
    try:
        if not args.logic_app_url or args.save_to_cosmos:
            await app_config.init()
        url = args.logic_app_url or app_config.get("TEAMS_COLLECTION_LOGIC_APP_URL", "")
        scope = args.logic_app_scope or app_config.get(
            "TEAMS_COLLECTION_LOGIC_APP_SCOPE", "https://management.core.windows.net/.default",
        )
        # Developer runs use their CLI login; Windows CLI startup can exceed 10 seconds.
        async with AzureCliCredential(process_timeout=60) as credential:
            return await collect_channel_history(
                request, args.output, url, token_scope=scope, credential=credential,
            )
    finally:
        try:
            await close_cosmos_client()
        finally:
            await close_credential()


async def main() -> None:
    args = parse_args()
    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")
    logging.getLogger("azure").setLevel(logging.WARNING)
    summary = await run(args)
    print(json.dumps({"output": str(args.output.resolve()), "summary": summary}))


if __name__ == "__main__":
    asyncio.run(main())
