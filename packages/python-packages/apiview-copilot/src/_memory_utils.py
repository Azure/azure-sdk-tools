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
from typing import List, Optional

from src._database_manager import DatabaseManager
from src._prompt_runner import run_prompt
from src._search_manager import SearchManager

logger = logging.getLogger(__name__)

# Sentinel ID used in the consolidation prompt to represent the candidate
# memory that has not been created yet.
_NEW_MEMORY_SENTINEL = "__new_memory__"


def check_for_duplicate_memory(
    *,
    raw_memory: dict,
    guideline_ids: List[str] = None,
) -> Optional[dict]:
    """Check whether a new memory is redundant with existing memories on the same guidelines.

    Fetches all memories already linked to the target guidelines and runs the
    ``consolidate_memories`` prompt to determine if the new memory should be
    merged with an existing one rather than created separately.

    Args:
        raw_memory: Dict of Memory fields (must include ``title`` and ``content``).
        guideline_ids: Guideline IDs (web-format or full URL) the new memory
            will be linked to. If empty or ``None``, dedup is skipped.

    Returns:
        A dict with ``existing_memory_id``, ``merged_title``, and ``merged_content``
        if a duplicate was found, or ``None`` to indicate no duplicate.
    """
    if not guideline_ids:
        return None

    title = raw_memory.get("title", "")
    content = raw_memory.get("content", "")
    if not title and not content:
        return None

    db = DatabaseManager.get_instance()

    # Collect all existing memories linked to the target guidelines.
    existing_memories: dict = {}  # memory_id -> {id, title, content}
    parent_title = None
    prefix = "https://azure.github.io/azure-sdk/"

    for gid in guideline_ids:
        try:
            if gid.startswith(prefix):
                gid = gid[len(prefix):]
            raw_g = db.guidelines.get(gid)
            if parent_title is None:
                parent_title = raw_g.get("title", gid)
            for mid in raw_g.get("related_memories", []):
                if mid not in existing_memories:
                    try:
                        raw_m = db.memories.get(mid)
                        existing_memories[mid] = {
                            "id": mid,
                            "title": raw_m.get("title", ""),
                            "content": raw_m.get("content", ""),
                        }
                    except Exception:
                        pass
        except Exception as e:
            logger.warning("Failed to fetch guideline %s for dedup: %s", gid, e)
            continue

    if not existing_memories:
        return None

    # Build the cluster: existing memories + the new candidate.
    memories_for_prompt = list(existing_memories.values())
    memories_for_prompt.append({
        "id": _NEW_MEMORY_SENTINEL,
        "title": title,
        "content": content,
    })

    try:
        raw_result = run_prompt(
            folder="other",
            filename="consolidate_memories",
            inputs={
                "parent_type": "guideline",
                "parent_title": parent_title or "Unknown",
                "memories": json.dumps(memories_for_prompt, indent=2),
            },
        )
        result = json.loads(raw_result)
    except Exception as e:
        logger.warning("Consolidation dedup prompt failed, proceeding with creation: %s", e)
        return None

    # Check if any merge group contains the new memory sentinel.
    for group in result.get("groups", []):
        if _NEW_MEMORY_SENTINEL in group.get("memory_ids", []):
            other_ids = [mid for mid in group["memory_ids"] if mid != _NEW_MEMORY_SENTINEL]
            if other_ids:
                return {
                    "existing_memory_id": other_ids[0],
                    "merged_title": group["merged_title"],
                    "merged_content": group["merged_content"],
                }

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

    # Only add examples whose content doesn't already exist on this memory.
    existing_contents = set()
    for eid in existing_memory.related_examples:
        try:
            raw_ex = db_manager.examples.get(eid)
            existing_contents.add(raw_ex.get("content", "").strip())
        except Exception:
            pass

    examples = []
    for ex in raw_examples:
        candidate = Example(**ex)
        if candidate.content.strip() in existing_contents:  # pylint: disable=no-member
            continue
        candidate.service = example_service
        candidate.id = f"{memory_id}-example-{uuid.uuid4().hex[:8]}"
        DatabaseManager.link_items(candidate, "example", existing_memory, "memory")
        examples.append(candidate)

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
    memory_snapshot = json.loads(json.dumps(raw_existing))
    saved_new_items = []
    saved_guideline_ids = []
    memory_saved = False

    def _rollback(error_msg: str):
        for container, item_id in saved_new_items:
            try:
                container.delete(item_id, run_indexer=False)
            except Exception as rb_err:
                logger.warning("Rollback warning: failed to delete %s: %s", item_id, rb_err)
        if memory_saved:
            try:
                db_manager.memories.client.upsert_item(memory_snapshot)
            except Exception as rb_err:
                logger.warning("Rollback warning: failed to restore memory %s: %s", existing_memory.id, rb_err)
        for g_db_id in saved_guideline_ids:
            try:
                db_manager.guidelines.client.upsert_item(guideline_snapshots[g_db_id])
            except Exception as rb_err:
                logger.warning("Rollback warning: failed to restore guideline %s: %s", g_db_id, rb_err)
        logger.warning(
            "Rolled back %d new item(s), %s memory update, and %d guideline update(s) after error: %s",
            len(saved_new_items),
            "1" if memory_saved else "0",
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
        memory_saved = True
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
    return {"success": [memory_id], "failures": {}, "merged": True}


def _build_clusters_for_guideline(guideline_id: str, db, memory_index: dict) -> List[tuple]:
    """Build a cluster from a single guideline's related memories."""
    try:
        raw_g = db.guidelines.get(guideline_id)
    except Exception as e:
        logger.warning("Failed to fetch guideline '%s': %s", guideline_id, e)
        return []

    mem_ids = raw_g.get("related_memories", [])
    for mid in mem_ids:
        if mid not in memory_index:
            try:
                memory_index[mid] = db.memories.get(mid)
            except Exception:
                pass
    mem_ids = [mid for mid in mem_ids if mid in memory_index]
    if len(mem_ids) >= 2:
        return [("guideline", raw_g["id"], raw_g.get("title", raw_g["id"]), mem_ids)]
    return []


def _build_clusters_for_example(example_id: str, db, memory_index: dict) -> List[tuple]:
    """Build a cluster from a single example's related memories."""
    try:
        raw_e = db.examples.get(example_id)
    except Exception as e:
        logger.warning("Failed to fetch example '%s': %s", example_id, e)
        return []

    mem_ids = raw_e.get("memory_ids", [])
    for mid in mem_ids:
        if mid not in memory_index:
            try:
                memory_index[mid] = db.memories.get(mid)
            except Exception:
                pass
    mem_ids = [mid for mid in mem_ids if mid in memory_index]
    if len(mem_ids) >= 2:
        return [("example", raw_e["id"], raw_e.get("title", raw_e["id"]), mem_ids)]
    return []


def _build_clusters_for_memory(memory_id: str, db, memory_index: dict) -> List[tuple]:
    """Build clusters from the parents of a single memory.

    Finds all guidelines and examples that reference the given memory,
    then returns clusters for those parents (if they have 2+ memories).
    """
    try:
        raw_mem = db.memories.get(memory_id)
    except Exception:
        logger.warning("Memory '%s' not found.", memory_id)
        return []

    if memory_id not in memory_index:
        memory_index[memory_id] = raw_mem

    from src._models import Memory

    mem = Memory(**raw_mem)
    clusters: List[tuple] = []

    for gid in mem.related_guidelines:
        clusters.extend(_build_clusters_for_guideline(gid, db, memory_index))

    for eid in mem.related_examples:
        clusters.extend(_build_clusters_for_example(eid, db, memory_index))

    return clusters


def find_consolidation_candidates(
    *,
    kind: str,
    ids: List[str],
) -> List[dict]:
    """Find clusters of memories linked to the given items that may contain duplicates.

    For each provided ID, looks up the item's related memories and calls an
    LLM to identify merge groups within each cluster.

    Args:
        kind: The type of item: ``"guideline"``, ``"example"``, or ``"memory"``.
        ids: One or more IDs of items of the given ``kind``.

    Returns:
        A list of consolidation actions, each containing:
        - ``parent_type``: "guideline" or "example"
        - ``parent_id``: The parent item's ID
        - ``parent_title``: The parent item's title
        - ``groups``: List of merge groups from the LLM, each with
          ``memory_ids``, ``merged_title``, ``merged_content``, ``reason``
    """
    db = DatabaseManager.get_instance()
    memory_index: dict = {}
    clusters: List[tuple] = []

    builder = {
        "guideline": _build_clusters_for_guideline,
        "example": _build_clusters_for_example,
        "memory": _build_clusters_for_memory,
    }
    build_fn = builder[kind]
    for item_id in ids:
        clusters.extend(build_fn(item_id, db, memory_index))

    if not clusters:
        return []

    # Deduplicate clusters that share the exact same set of memory IDs
    # (multiple parents may link the same memory set)
    seen_memory_sets = set()
    unique_clusters = []
    for parent_type, parent_id, parent_title, mem_ids in clusters:
        key = frozenset(mem_ids)
        if key not in seen_memory_sets:
            seen_memory_sets.add(key)
            unique_clusters.append((parent_type, parent_id, parent_title, mem_ids))

    actions = []
    for parent_type, parent_id, parent_title, mem_ids in unique_clusters:
        memories_for_prompt = []
        for mid in mem_ids:
            m = memory_index[mid]
            memories_for_prompt.append({"id": m["id"], "title": m.get("title", ""), "content": m.get("content", "")})

        try:
            raw_result = run_prompt(
                folder="other",
                filename="consolidate_memories",
                inputs={
                    "parent_type": parent_type,
                    "parent_title": parent_title,
                    "memories": json.dumps(memories_for_prompt, indent=2),
                },
            )
            result = json.loads(raw_result)
        except Exception as e:
            logger.warning(
                "Consolidation prompt failed for %s '%s': %s",
                parent_type,
                parent_id,
                e,
            )
            continue

        groups = result.get("groups", [])
        if groups:
            actions.append(
                {
                    "parent_type": parent_type,
                    "parent_id": parent_id,
                    "parent_title": parent_title,
                    "groups": groups,
                }
            )

    return actions


def apply_consolidation(actions: List[dict]) -> dict:
    """Apply consolidation actions: merge duplicate memories and transfer links.

    For each merge group:
    1. The first memory in the group is the **survivor**. Its title and content
       are updated to the merged values.
    2. All links (guidelines, examples, other memories) from redundant memories
       are transferred to the survivor.
    3. Redundant memories are soft-deleted.

    Args:
        actions: List of consolidation actions from ``find_consolidation_candidates()``.

    Returns:
        ``{"merged": int, "deleted": int, "errors": [str]}``
    """
    from src._models import Example, Guideline, Memory
    from src._utils import guideline_id_from_db

    db = DatabaseManager.get_instance()
    merged_count = 0
    deleted_count = 0
    errors = []

    # Track which memory IDs have already been deleted in a prior group
    # to avoid double-processing when multiple parents share memory clusters.
    already_deleted = set()

    for action in actions:
        for group in action["groups"]:
            memory_ids = group["memory_ids"]
            # Skip memory IDs that were already deleted
            memory_ids = [mid for mid in memory_ids if mid not in already_deleted]
            if len(memory_ids) < 2:
                continue

            survivor_id = memory_ids[0]
            redundant_ids = memory_ids[1:]

            # Fetch the survivor memory
            try:
                raw_survivor = db.memories.get(survivor_id)
                survivor = Memory(**raw_survivor)
            except Exception as e:
                errors.append(f"Failed to fetch survivor memory {survivor_id}: {e}")
                continue

            # Update survivor with merged content
            survivor.title = group["merged_title"]
            survivor.content = group["merged_content"]

            # Transfer links from each redundant memory to the survivor
            for rid in redundant_ids:
                try:
                    raw_redundant = db.memories.get(rid)
                    redundant = Memory(**raw_redundant)
                except Exception as e:
                    errors.append(f"Failed to fetch redundant memory {rid}: {e}")
                    continue

                # Transfer guideline links
                for gid in redundant.related_guidelines:
                    if gid not in survivor.related_guidelines:
                        survivor.related_guidelines.append(gid)  # pylint: disable=no-member
                    # Update the guideline to reference the survivor instead
                    try:
                        raw_g = db.guidelines.get(gid)
                        guideline = Guideline(**raw_g)
                        if rid in guideline.related_memories:
                            guideline.related_memories.remove(rid)  # pylint: disable=no-member
                        if survivor_id not in guideline.related_memories:
                            guideline.related_memories.append(survivor_id)  # pylint: disable=no-member
                        db.guidelines.upsert(guideline.id, data=guideline, run_indexer=False)
                    except Exception as e:
                        display_id = guideline_id_from_db(gid)
                        errors.append(f"Failed to update guideline {display_id} for memory {rid}: {e}")

                # Transfer example links
                for eid in redundant.related_examples:
                    if eid not in survivor.related_examples:
                        survivor.related_examples.append(eid)  # pylint: disable=no-member
                    # Update the example to reference the survivor instead
                    try:
                        raw_e = db.examples.get(eid)
                        example = Example(**raw_e)
                        if rid in example.memory_ids:
                            example.memory_ids.remove(rid)  # pylint: disable=no-member
                        if survivor_id not in example.memory_ids:
                            example.memory_ids.append(survivor_id)  # pylint: disable=no-member
                        db.examples.upsert(example.id, data=example, run_indexer=False)
                    except Exception as e:
                        errors.append(f"Failed to update example {eid} for memory {rid}: {e}")

                # Transfer memory-to-memory links
                for mid in redundant.related_memories:
                    if mid == survivor_id or mid in redundant_ids:
                        continue
                    if mid not in survivor.related_memories:
                        survivor.related_memories.append(mid)
                    # Update the linked memory to reference the survivor instead
                    try:
                        raw_m = db.memories.get(mid)
                        linked = Memory(**raw_m)
                        if rid in linked.related_memories:
                            linked.related_memories.remove(rid)
                        if survivor_id not in linked.related_memories:
                            linked.related_memories.append(survivor_id)
                        db.memories.upsert(linked.id, data=linked, run_indexer=False)
                    except Exception as e:
                        errors.append(f"Failed to update linked memory {mid} for memory {rid}: {e}")

                # Soft-delete the redundant memory
                try:
                    db.memories.delete(rid, run_indexer=False)
                    already_deleted.add(rid)
                    deleted_count += 1
                except Exception as e:
                    errors.append(f"Failed to delete redundant memory {rid}: {e}")

            # Remove references to deleted memories from the survivor
            survivor.related_memories = [
                mid for mid in survivor.related_memories if mid not in already_deleted
            ]

            # Save the survivor
            try:
                db.memories.upsert(survivor.id, data=survivor, run_indexer=False)
                merged_count += 1
            except Exception as e:
                errors.append(f"Failed to save survivor memory {survivor_id}: {e}")

    # Trigger indexers once at the end
    SearchManager.run_indexers()

    return {"merged": merged_count, "deleted": deleted_count, "errors": errors}
