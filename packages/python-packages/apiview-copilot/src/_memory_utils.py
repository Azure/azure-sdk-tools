# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Shared memory deduplication utilities for mention and thread-resolution workflows.
"""

import json
import logging
from typing import Optional

from src._database_manager import DatabaseManager
from src._prompt_runner import run_prompt
from src._search_manager import SearchManager

logger = logging.getLogger(__name__)

# Minimum semantic reranker score to consider two memories as duplicates.
# Azure AI Search reranker scores range from 0 to 4.
# TJP: Seems arbitrary...
SIMILARITY_THRESHOLD = 3.0


def check_for_duplicate_memory(
    *,
    raw_memory: dict,
    language: str,
) -> Optional[dict]:
    """Check whether a semantically similar memory already exists.

    Searches the knowledge base for existing memories that match the new
    memory's title and content. If a match above the similarity threshold
    is found, runs a merge prompt and returns the merge result.

    Args:
        raw_memory: Dict of Memory fields (must include ``title`` and ``content``).
        language: Language to scope the search.

    Returns:
        A dict with ``existing_memory_id``, ``merged_title``, and ``merged_content``
        if a duplicate was found, or ``None`` to indicate no duplicate.
    """
    title = raw_memory.get("title", "")
    content = raw_memory.get("content", "")
    query = f"{title} {content}".strip()
    if not query:
        return None

    try:
        search_manager = SearchManager(language=language)
        # TJP: Just 5? What if there are more?
        # TJP: Also, wouldn't it make as much or more sense to simply look at any memories already associated with whatever THIS memory will be linked to (e.g. the same guideline)? Why search the entire KB?
        search_results = search_manager.search_memories(query, top=5)
    except Exception as e:
        logger.warning("Memory dedup search failed, proceeding with creation: %s", e)
        return None

    # Find the best match above the similarity threshold
    # TJP: Wow this logic is very wrong! There could be many matches!
    best_match = None
    for result in search_results:
        score = result.reranker_score
        if score is not None and score >= SIMILARITY_THRESHOLD:
            if best_match is None or score > best_match.reranker_score:
                best_match = result

    if best_match is None:
        return None

    # Run merge prompt
    existing_memory_json = json.dumps({"title": best_match.title, "content": best_match.content})
    new_memory_json = json.dumps({"title": title, "content": content})

    try:
        raw_result = run_prompt(
            folder="mention",
            filename="merge_memories",
            inputs={
                "existing_memory": existing_memory_json,
                "new_memory": new_memory_json,
            },
        )
        merged = json.loads(raw_result)
        return {
            "existing_memory_id": best_match.id,
            "merged_title": merged["title"],
            "merged_content": merged["content"],
        }
    except Exception as e:
        logger.warning("Memory merge prompt failed, proceeding with creation: %s", e)
        return None


def merge_and_save_memory(
    *,
    merge_result: dict,
    raw_memory: dict,
    guideline_ids: list[str],
    raw_examples: list[dict],
    example_service: Optional[str] = None,
) -> dict:
    """Merge a new memory into an existing one and save all linked items.

    Updates the existing memory's title and content with the merged values,
    adds any new guideline/example links, and persists everything to the database.

    Args:
        merge_result: Dict from ``check_for_duplicate_memory()`` with
            ``existing_memory_id``, ``merged_title``, ``merged_content``.
        raw_memory: The original raw_memory dict (for source metadata fields).
        guideline_ids: Guideline IDs to link.
        raw_examples: Example dicts to create and link.
        example_service: Value for ``example.service``.

    Returns:
        ``{"success": [memory_id], "failures": {}}``
    """
    import uuid

    from src._models import Example, Guideline, Memory
    from src._utils import guideline_id_to_db

    db_manager = DatabaseManager.get_instance()
    memory_id = merge_result["existing_memory_id"]

    # Fetch the existing memory from the database
    try:
        raw_existing = db_manager.memories.get(memory_id)
        existing_memory = Memory(**raw_existing)
    except Exception as e:
        logger.warning("Failed to fetch existing memory %s for merge, creating new: %s", memory_id, e)
        return db_manager.save_memory_with_links(
            raw_memory=raw_memory,
            guideline_ids=guideline_ids,
            raw_examples=raw_examples,
            example_service=example_service,
        )

    # Update the existing memory with merged content
    existing_memory.title = merge_result["merged_title"]
    existing_memory.content = merge_result["merged_content"]

    # Build examples linked to the existing memory
    examples = [Example(**ex) for ex in raw_examples]
    for example in examples:
        example.service = example_service
        example.id = f"{memory_id}-example-{uuid.uuid4().hex[:8]}"
        DatabaseManager.link_items(example, "example", existing_memory, "memory")

    # Fetch and link guidelines that aren't already linked
    guidelines = []
    guideline_snapshots = {}
    prefix = "https://azure.github.io/azure-sdk/"
    for gid in guideline_ids:
        try:
            if gid.startswith(prefix):
                gid = gid[len(prefix):]
            raw_guideline = db_manager.guidelines.get(gid)
            guideline_snapshots[raw_guideline["id"]] = json.loads(json.dumps(raw_guideline))
            guideline = Guideline(**raw_guideline)
            _, _, changed = DatabaseManager.link_items(guideline, "guideline", existing_memory, "memory")
            if changed:
                guidelines.append(guideline)
        except Exception as e:
            logger.warning("Error retrieving guideline %s during merge: %s", gid, e)
            continue

    # ── Save with rollback ───────────────────────────────────────────
    saved_new_items = []
    saved_guideline_ids = []

    def _rollback(error_msg: str):
        for container, item_id in saved_new_items:
            try:
                container.delete(item_id, run_indexer=False)
            except Exception as rb_err:
                logger.warning("Rollback warning: failed to delete %s: %s", item_id, rb_err)
        for g_db_id in saved_guideline_ids:
            try:
                db_manager.guidelines.client.upsert_item(guideline_snapshots[g_db_id])
            except Exception as rb_err:
                logger.warning("Rollback warning: failed to restore guideline %s: %s", g_db_id, rb_err)
        logger.warning(
            "Rolled back %d new item(s) and %d guideline update(s) after error: %s",
            len(saved_new_items),
            len(saved_guideline_ids),
            error_msg,
        )

    # 1. Save new examples
    for example in examples:
        try:
            db_manager.examples.upsert(example.id, data=example, run_indexer=False)
            saved_new_items.append((db_manager.examples, example.id))
        except Exception as e:
            _rollback(str(e))
            return {"success": [], "failures": {example.id: str(e)}}

    # 2. Update existing memory
    try:
        db_manager.memories.upsert(existing_memory.id, data=existing_memory, run_indexer=False)
    except Exception as e:
        _rollback(str(e))
        return {"success": [], "failures": {existing_memory.id: str(e)}}

    # 3. Update guidelines with new links
    for guideline in guidelines:
        db_id = guideline_id_to_db(guideline.id)
        try:
            db_manager.guidelines.upsert(guideline.id, data=guideline, run_indexer=False)
            saved_guideline_ids.append(db_id)
        except Exception as e:
            _rollback(str(e))
            return {"success": [], "failures": {guideline.id: str(e)}}

    SearchManager.run_indexers()
    return {"success": [memory_id], "failures": {}}
