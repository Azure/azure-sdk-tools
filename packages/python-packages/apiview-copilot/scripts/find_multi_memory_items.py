#!/usr/bin/env python3
# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Find knowledge base items (guidelines, examples, memories) that have more than
one related memory. Output is intended to feed into:

    avc kb consolidate-memories --kind <type> --ids <id1> <id2> ...

Usage:
    python scripts/find_multi_memory_items.py [--language <lang>] [--min-memories N]
"""

import argparse
import os
import sys

_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
_ROOT = os.path.dirname(_SCRIPT_DIR)
sys.path.insert(0, _ROOT)

from src._database_manager import DatabaseManager
from src._utils import guideline_id_from_db


def _query_all(container, language_filter=None):
    if language_filter:
        query = "SELECT * FROM c WHERE (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false) AND c.language = @lang"
        params = [{"name": "@lang", "value": language_filter}]
    else:
        query = "SELECT * FROM c WHERE NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false"
        params = None
    return list(
        container.client.query_items(query=query, parameters=params, enable_cross_partition_query=True)
    )


def main():
    parser = argparse.ArgumentParser(description="Find KB items with multiple related memories.")
    parser.add_argument("--language", type=str, default=None, help="Filter by language (e.g. python, dotnet).")
    parser.add_argument("--min-memories", type=int, default=2, help="Minimum number of related memories to include (default: 2).")
    args = parser.parse_args()

    db = DatabaseManager.get_instance()

    print("Loading knowledge base items...")
    guidelines = _query_all(db.guidelines, args.language)
    examples = _query_all(db.examples, args.language)
    memories = _query_all(db.memories, args.language)
    print(f"  guidelines: {len(guidelines)}, examples: {len(examples)}, memories: {len(memories)}\n")

    results = []

    for item in guidelines:
        related = item.get("related_memories", [])
        if len(related) >= args.min_memories:
            results.append(("guideline", guideline_id_from_db(item["id"]), len(related)))

    for item in examples:
        related = item.get("memory_ids", [])
        if len(related) >= args.min_memories:
            results.append(("example", item["id"], len(related)))

    for item in memories:
        related = item.get("related_memories", [])
        if len(related) >= args.min_memories:
            results.append(("memory", item["id"], len(related)))

    # Sort by count descending
    results.sort(key=lambda x: x[2], reverse=True)

    if not results:
        print("No items found with multiple related memories.")
        return

    print(f"Found {len(results)} item(s) with >= {args.min_memories} related memories:\n")
    print(f"{'Type':<12} {'# Memories':<12} {'ID'}")
    print(f"{'-'*12} {'-'*12} {'-'*60}")
    for kind, item_id, count in results:
        print(f"{kind:<12} {count:<12} {item_id}")

    # Group by type for easy copy-paste into avc commands
    print("\n--- Commands for consolidation ---\n")
    by_type = {}
    for kind, item_id, _ in results:
        by_type.setdefault(kind, []).append(item_id)

    for kind, ids in by_type.items():
        ids_str = " ".join(ids)
        print(f"avc kb consolidate-memories --kind {kind} --ids {ids_str}")


if __name__ == "__main__":
    main()
