# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Script to find the top 10 guidelines with the most memories attached.

Queries Cosmos DB for all guidelines, ranks them by the count of related_memories,
and prints each guideline along with its associated memory details.

Usage:
    python scripts/top_guidelines_by_memories.py
"""

import sys
import os

# Ensure the project root is on sys.path so `src` imports work
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from src._database_manager import DatabaseManager


def main():
    db = DatabaseManager.get_instance()

    # Query all non-deleted guidelines that have at least one related memory
    query = (
        "SELECT c.id, c.title, c.content, c.language, c.tags, c.related_memories "
        "FROM c WHERE ARRAY_LENGTH(c.related_memories) > 0 "
        "AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)"
    )
    guidelines = list(
        db.guidelines.client.query_items(query=query, enable_cross_partition_query=True)
    )

    # Sort by number of related memories, descending
    guidelines.sort(key=lambda g: len(g.get("related_memories", [])), reverse=True)

    # Take top 10
    top_guidelines = guidelines[:10]

    if not top_guidelines:
        print("No guidelines with related memories found.")
        return

    # Pre-fetch all referenced memory IDs
    all_memory_ids = set()
    for g in top_guidelines:
        all_memory_ids.update(g.get("related_memories", []))

    # Fetch memories in bulk
    memories_cache = {}
    for memory_id in all_memory_ids:
        try:
            memory = db.memories.get(memory_id)
            memories_cache[memory_id] = memory
        except Exception:
            memories_cache[memory_id] = None

    # Display results
    print("=" * 80)
    print(f"Top {len(top_guidelines)} Guidelines by Number of Related Memories")
    print("=" * 80)

    for rank, guideline in enumerate(top_guidelines, start=1):
        memory_ids = guideline.get("related_memories", [])
        print(f"\n{'─' * 80}")
        print(f"#{rank} — {len(memory_ids)} memories")
        print(f"  ID:       {guideline['id']}")
        print(f"  Title:    {guideline.get('title', 'N/A')}")
        print(f"  Language: {guideline.get('language', 'general')}")
        if guideline.get("tags"):
            print(f"  Tags:     {', '.join(guideline['tags'])}")
        print("\n  Guideline Content:")
        for line in guideline.get("content", "").splitlines():
            print(f"    {line}")

        print(f"\n  Related Memories ({len(memory_ids)}):")
        for mid in memory_ids:
            mem = memories_cache.get(mid)
            if mem is None:
                print(f"    - [{mid}] (not found or deleted)")
            else:
                print(f"    - [{mem['id']}] {mem.get('title', 'N/A')}")
                for line in mem.get("content", "").splitlines():
                    print(f"        {line}")
                if mem.get("language"):
                    print(f"        Language: {mem['language']}")
                if mem.get("source"):
                    print(f"        Source: {mem['source']}")

    print(f"\n{'=' * 80}")


if __name__ == "__main__":
    main()
