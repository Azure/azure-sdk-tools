# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Backfill CorrelationId for Diagnostic comments in the APIView Comments container.

Finds all comments where CommentSource == "Diagnostic" and CorrelationId is null,
parses the diagnostic ID from the comment text (expected format: "... [diagnosticId]"),
and sets CorrelationId to the parsed diagnostic ID.

By default, runs in dry-run mode. Pass --apply to write changes.

Usage:
    python scripts/backfill_diagnostic_correlation_ids.py                     # dry-run, all environments
    python scripts/backfill_diagnostic_correlation_ids.py --apply             # apply to all environments
    python scripts/backfill_diagnostic_correlation_ids.py --env staging       # dry-run, staging only
    python scripts/backfill_diagnostic_correlation_ids.py --env staging --apply
"""

import argparse
import re
import sys
from pathlib import Path

from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosHttpResponseError

# Add project root to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))
from src._credential import get_credential
from src._utils import get_language_pretty_name

# Cosmos account mapping per environment
APIVIEW_COSMOS_ACCOUNTS = {
    "production": "apiview-cosmos",
    "staging": "apiviewstaging",
    "uxtest": "apiviewuitest",
}

DB_NAME = "APIViewV2"
CONTAINER_NAME = "Comments"

# Pattern to extract the diagnostic ID from brackets in comment text.
# Matches [someId] but NOT markdown links like [text](url).
DIAGNOSTIC_ID_PATTERN = re.compile(r"\[([^\[\]]+)\](?!\()")


def get_cosmos_client(environment: str, container_name: str = CONTAINER_NAME):
    """Returns a Cosmos DB container client for the given environment and container."""
    account_name = APIVIEW_COSMOS_ACCOUNTS.get(environment)
    if not account_name:
        valid = ", ".join(sorted(APIVIEW_COSMOS_ACCOUNTS.keys()))
        raise ValueError(f"Unknown environment '{environment}'. Valid options: {valid}")

    cosmos_url = f"https://{account_name}.documents.azure.com:443/"
    credential = get_credential()
    client = CosmosClient(url=cosmos_url, credential=credential)
    database = client.get_database_client(DB_NAME)
    return database.get_container_client(container_name)


def get_review_ids_for_language(environment: str, language: str) -> set[str]:
    """Query the Reviews container and return the set of review IDs matching the given language."""
    reviews_container = get_cosmos_client(environment, container_name="Reviews")
    query = "SELECT c.id, c.Language, c.PackageName FROM c WHERE c.Language = @lang"
    results = list(
        reviews_container.query_items(
            query=query,
            parameters=[{"name": "@lang", "value": language}],
            enable_cross_partition_query=True,
        )
    )
    return {r["id"] for r in results}


def parse_diagnostic_id(comment_text: str) -> str | None:
    """
    Parse the diagnostic ID from the comment text.
    The diagnostic ID is expected inside square brackets, e.g. "Some message [diagnosticId]".
    Returns the last bracketed value found, or None if no match.
    """
    if not comment_text:
        return None
    matches = DIAGNOSTIC_ID_PATTERN.findall(comment_text)
    if matches:
        return matches[-1].strip()
    return None


def process_environment(environment: str, apply: bool, language: str | None = None) -> dict:
    """
    Process a single environment: find Diagnostic comments with null CorrelationId,
    parse the diagnostic ID, and optionally update.

    Returns a summary dict with counts.
    """
    print(f"\n{'=' * 60}")
    print(f"  Environment: {environment}")
    print(f"  Language:    {language or 'all'}")
    print(f"  Mode:        {'APPLY' if apply else 'DRY-RUN'}")
    print(f"{'=' * 60}")

    try:
        container = get_cosmos_client(environment)
    except (CosmosHttpResponseError, ValueError) as e:
        print(f"  ERROR: Could not connect to {environment}: {e}")
        return {"environment": environment, "error": str(e)}

    # Query for Diagnostic comments with null or missing CorrelationId
    query = """
        SELECT c.id, c.CommentText, c.CorrelationId, c.ReviewId, c.ElementId
        FROM c
        WHERE c.CommentSource = 'Diagnostic'
        AND (NOT IS_DEFINED(c.CorrelationId) OR IS_NULL(c.CorrelationId) OR c.CorrelationId = '')
    """

    print("  Querying for Diagnostic comments with null CorrelationId...")
    try:
        items = list(container.query_items(query=query, enable_cross_partition_query=True))
    except CosmosHttpResponseError as e:
        print(f"  ERROR querying: {e}")
        return {"environment": environment, "error": str(e)}

    # If a language filter is specified, restrict to reviews for that language
    if language:
        print(f"  Fetching review IDs for language '{language}'...")
        try:
            review_ids = get_review_ids_for_language(environment, language)
        except CosmosHttpResponseError as e:
            print(f"  ERROR fetching reviews: {e}")
            return {"environment": environment, "error": str(e)}
        print(f"  Found {len(review_ids)} review(s) for language '{language}'.")
        items = [item for item in items if item.get("ReviewId") in review_ids]

    print(f"  Found {len(items)} comment(s) to process.")

    updated = 0
    skipped = 0
    parse_failures = 0

    for item in items:
        comment_id = item["id"]
        comment_text = item.get("CommentText", "")
        review_id = item.get("ReviewId", "")
        element_id = item.get("ElementId", "")

        diagnostic_id = parse_diagnostic_id(comment_text)

        print(f"  {'UPDATE' if apply and diagnostic_id else 'WOULD UPDATE' if diagnostic_id else 'SKIP'}: {comment_id}")
        print(f"    ReviewId:       {review_id}")
        print(f"    ElementId:      {element_id}")
        print(f"    CommentText:    {comment_text}")
        print(f"    CorrelationId:  {diagnostic_id if diagnostic_id else 'NULL'}")

        if not diagnostic_id:
            parse_failures += 1
            continue

        if apply:
            try:
                # Read the full item to get all fields (including system properties)
                full_item = container.read_item(item=comment_id, partition_key=comment_id)
                full_item["CorrelationId"] = diagnostic_id
                container.upsert_item(full_item)
                updated += 1
            except CosmosHttpResponseError as e:
                print(f"    ERROR updating: {e}")
                skipped += 1
        else:
            updated += 1  # count as "would update" in dry-run

    summary = {
        "environment": environment,
        "total_found": len(items),
        "updated": updated,
        "skipped": skipped,
        "parse_failures": parse_failures,
        "applied": apply,
    }

    print(f"\n  Summary for {environment}:")
    print(f"    Total found:    {len(items)}")
    print(f"    {'Updated' if apply else 'Would update'}: {updated}")
    print(f"    Parse failures: {parse_failures}")
    print(f"    Skipped/errors: {skipped}")

    return summary


def main():
    parser = argparse.ArgumentParser(
        description="Backfill CorrelationId for Diagnostic comments in APIView Cosmos DB."
    )
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Actually write changes to the database. Without this flag, runs in dry-run mode.",
    )
    parser.add_argument(
        "--env",
        type=str,
        choices=list(APIVIEW_COSMOS_ACCOUNTS.keys()),
        help="Process only a specific environment. If omitted, processes all environments.",
    )
    parser.add_argument(
        "--language",
        type=str,
        help="Filter to comments belonging to reviews of a specific language (e.g. 'Python', 'Java').",
    )
    args = parser.parse_args()

    environments = [args.env] if args.env else list(APIVIEW_COSMOS_ACCOUNTS.keys())

    if not args.apply:
        print("*** DRY-RUN MODE — no changes will be written. Pass --apply to write. ***")

    summaries = []
    for env in environments:
        summary = process_environment(env, apply=args.apply, language=args.language)
        summaries.append(summary)

    # Final report
    print(f"\n{'=' * 60}")
    print("  FINAL REPORT")
    print(f"{'=' * 60}")
    for s in summaries:
        env = s["environment"]
        if "error" in s:
            print(f"  {env}: ERROR - {s['error']}")
        else:
            action = "Updated" if s["applied"] else "Would update"
            print(f"  {env}: Found {s['total_found']}, {action} {s['updated']}, "
                  f"Parse failures {s['parse_failures']}, Skipped {s['skipped']}")

    if not args.apply:
        print("\n*** This was a DRY RUN. Re-run with --apply to write changes. ***")


if __name__ == "__main__":
    main()
