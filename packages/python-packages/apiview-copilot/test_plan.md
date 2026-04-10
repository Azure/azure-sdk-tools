# Test Plan: Guideline-Scoped Memory Deduplication

## What Changed

`check_for_duplicate_memory` no longer searches the entire KB via semantic search. Instead, it fetches all existing memories linked to the target guidelines and uses the `consolidate_memories` prompt to determine if the new memory should be merged.

## Prerequisites

```bash
# Activate environment
pyenv shell 3.12.9

# Ensure dependencies are installed
pip install -r dev_requirements.txt

# Ensure .env is configured with ENVIRONMENT_NAME
```

---

## 1. Unit Tests (automated)

```bash
pytest tests/memory_utils_test.py tests/update_kb_workflow_test.py -v
```

Expected: 26 tests pass.

---

## 2. Find Candidates Script

Verify the discovery script works and produces output:

```bash
python scripts/find_multi_memory_items.py --language python
python scripts/find_multi_memory_items.py --language python --min-memories 3
```

Expected: Table of items with multiple related memories, plus ready-to-paste `avc kb consolidate-memories` commands.

---

## 3. Consolidate Memories (dry run)

Pick a guideline from the script output that has several related memories and run:

```bash
avc kb consolidate-memories --kind guideline --ids "<guideline-id>"
```

Expected: Shows merge groups with merged titles, content, and reasons. No changes are made.

---

## 4. Consolidate Memories (apply)

Using the same guideline:

```bash
avc kb consolidate-memories --kind guideline --ids "<guideline-id>" --apply
```

Expected: Survivor memory is updated, redundant memories are deleted, back-links are fixed. Verify with:

```bash
avc db get --container guidelines --id "<guideline-id>"
```

Check that `related_memories` no longer contains the deleted IDs.

---

## 5. Mention Workflow — Duplicate Memory

Test that a mention creating a memory that duplicates an existing one merges instead of creating a new entry.

### Setup
Pick a guideline that already has a memory. Note the guideline ID and the existing memory's title/content.

### Test
Use `avc agent mention` with a payload that would produce a memory saying essentially the same thing as the existing one, referencing the same guideline.

Expected:
- The response summary should indicate the memory was merged, not newly created.
- The existing memory's content should be updated (verify with `avc db get --container memories --id "<memory-id>"`).
- No new memory ID should appear in the guideline's `related_memories`.

---

## 6. Mention Workflow — Novel Memory

Test that a genuinely new memory is created normally when it doesn't duplicate existing ones.

### Test
Use `avc agent mention` with a payload that produces a memory with distinct content from anything on the target guideline.

Expected:
- A new memory is created.
- The guideline's `related_memories` list grows by one.
- The new memory's `related_guidelines` includes the guideline ID.

---

## 7. Mention Workflow — No Guideline IDs

Test that when the conversation doesn't reference any guidelines, dedup is skipped entirely.

### Test
Use `avc agent mention` with a payload where no guideline IDs are extracted from the conversation.

Expected:
- A new memory is created (no dedup attempted).

---

## 8. Thread Resolution — Same Scenarios

Repeat scenarios 5–7 using thread resolution instead of mentions. Thread resolution also calls `check_for_duplicate_memory`.

```bash
avc agent resolve-thread ...
```

Expected: Same dedup behavior as the mention workflow.

---

## 9. Verify Back-Links After Consolidation

After running scenario 4, verify bidirectional link integrity:

```bash
avc kb check-links --language python
```

Expected: No broken or one-way links related to the consolidated memories.

---

## 10. Edge Cases

| Scenario | Expected |
|---|---|
| Guideline has 0 existing memories | Dedup skipped, new memory created normally |
| Guideline has 1 existing memory | Consolidation prompt runs with 2 memories (1 existing + 1 new) |
| Multiple guideline IDs in plan | Memories from all guidelines collected into one cluster |
| Guideline ID uses full URL format | URL prefix stripped, guideline fetched correctly |
| LLM prompt fails | Warning logged, falls back to creating new memory |
| Existing memory can't be fetched for merge | Falls back to `save_memory_with_links` |
