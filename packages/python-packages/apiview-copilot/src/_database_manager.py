# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module for managing the database connections and operations for APIView Copilot.
"""

import time
from enum import Enum
from typing import Any, Optional

from azure.cosmos import CosmosClient
from azure.cosmos.exceptions import CosmosResourceNotFoundError
from azure.search.documents.indexes import SearchIndexerClient
from pydantic import BaseModel
from src._credential import get_credential
from src._settings import SettingsManager
from src._utils import guideline_id_to_db

# Canonical relationship field mapping between KB item types.
# Key: (type_a, type_b) -> (field_on_type_a, field_on_type_b)
RELATIONSHIP_FIELDS = {
    ("guideline", "memory"): ("related_memories", "related_guidelines"),
    ("guideline", "example"): ("related_examples", "guideline_ids"),
    ("guideline", "guideline"): ("related_guidelines", "related_guidelines"),
    ("memory", "example"): ("related_examples", "memory_ids"),
    ("memory", "guideline"): ("related_guidelines", "related_memories"),
    ("memory", "memory"): ("related_memories", "related_memories"),
    ("example", "guideline"): ("guideline_ids", "related_examples"),
    ("example", "memory"): ("memory_ids", "related_examples"),
}


class ContainerNames(Enum):
    """Enumeration for container names in the database."""

    GUIDELINES = "guidelines"
    MEMORIES = "memories"
    EXAMPLES = "examples"
    REVIEW_JOBS = "review-jobs"
    METRICS = "metrics"
    EVALS = "evals"

    @classmethod
    def values(cls) -> list[str]:
        """Return a list of container names."""
        return [name.value for name in cls]

    @classmethod
    def data_containers(cls) -> list[str]:
        """Return the knowledge base container names (those with Azure AI Search indexers)."""
        return [cls.GUIDELINES.value, cls.EXAMPLES.value, cls.MEMORIES.value]


class DatabaseManager:
    """Manager for Azure Cosmos DB operations."""

    _instances: dict = {}

    @classmethod
    def get_instance(cls, force_new: bool = False, environment: Optional[str] = None) -> "DatabaseManager":
        """
        Returns a singleton instance of DatabaseManager, keyed by environment.
        At most two instances exist: one for 'production' and one for 'staging'.
        """
        settings = SettingsManager(environment=environment)
        key = settings.label
        if key not in cls._instances or force_new:
            cls._instances[key] = cls(environment=environment)
        return cls._instances[key]

    def __init__(self, environment: Optional[str] = None):
        settings = SettingsManager(environment=environment)
        self.environment = settings.label
        db_name = settings.get("COSMOS_DB_NAME")
        endpoint = settings.get("COSMOS_ENDPOINT")
        credential = get_credential()

        self.client = CosmosClient(endpoint, credential=credential)
        self.database = self.client.get_database_client(db_name)
        self.containers = {}

    def get_container_client(self, name: str) -> "BasicContainer":
        """Get a container client by name."""
        # Decide which container class to use
        if name not in self.containers:
            if name == ContainerNames.REVIEW_JOBS.value:
                self.containers[name] = ReviewJobsContainer(self, name)
            elif name == ContainerNames.METRICS.value:
                self.containers[name] = MetricsContainer(self, name)
            elif name == ContainerNames.EVALS.value:
                self.containers[name] = EvalsContainer(self, name)
            elif name == ContainerNames.GUIDELINES.value:
                self.containers[name] = GuidelinesContainer(self, name)
            else:
                self.containers[name] = BasicContainer(self, name)
        return self.containers[name]

    @property
    def guidelines(self):
        """Get the guidelines container client."""
        return self.get_container_client(ContainerNames.GUIDELINES.value)

    @property
    def memories(self):
        """Get the memories container client."""
        return self.get_container_client(ContainerNames.MEMORIES.value)

    @property
    def examples(self):
        """Get the examples container client."""
        return self.get_container_client(ContainerNames.EXAMPLES.value)

    @property
    def review_jobs(self):
        """Get the review jobs container client."""
        return self.get_container_client(ContainerNames.REVIEW_JOBS.value)

    @property
    def metrics(self):
        """Get the metrics container client."""
        return self.get_container_client(ContainerNames.METRICS.value)

    @property
    def evals(self):
        """Get the evaluations container client."""
        return self.get_container_client(ContainerNames.EVALS.value)

    @staticmethod
    def link_items(item_a, type_a: str, item_b, type_b: str):
        """Add bidirectional cross-references between two KB items in memory.

        Items must be Pydantic model instances (Guideline, Memory, or Example).
        This only modifies the in-memory objects; the caller is responsible for
        persisting changes to the database.

        Returns:
            tuple: (field_a, field_b, changed) where changed is True if any new
            references were added.

        Raises:
            ValueError: If the (type_a, type_b) pair has no defined relationship.
        """
        key = (type_a, type_b)
        if key not in RELATIONSHIP_FIELDS:
            raise ValueError(
                f"No relationship defined for ({type_a!r}, {type_b!r}). "
                f"Valid pairs: {sorted(RELATIONSHIP_FIELDS.keys())}"
            )
        field_a, field_b = RELATIONSHIP_FIELDS[key]
        id_a = item_a.id
        id_b = item_b.id

        changed = False
        refs_a = getattr(item_a, field_a)
        if id_b not in refs_a:
            refs_a.append(id_b)
            changed = True

        refs_b = getattr(item_b, field_b)
        if id_a not in refs_b:
            refs_b.append(id_a)
            changed = True

        return field_a, field_b, changed

    def link_and_save(self, type_a: str, id_a: str, type_b: str, id_b: str, *, run_indexer: bool = True):
        """Fetch two KB items, link them bidirectionally, and save with rollback.

        Both items are loaded as Pydantic models to ensure format validation
        (e.g., guideline ID normalization) and saved via ``model_dump_db()``
        for correct DB encoding.

        Returns:
            dict with keys ``field_a``, ``field_b``, ``stored_id_a``,
            ``stored_id_b``, and ``changed``.

        Raises:
            CosmosResourceNotFoundError: If either item does not exist.
            ValueError: If the type pair is invalid.
        """
        import json as _json

        from src._models import Example, Guideline, Memory

        model_map = {
            "guideline": Guideline,
            "memory": Memory,
            "example": Example,
        }
        containers = {
            "guideline": self.guidelines,
            "memory": self.memories,
            "example": self.examples,
        }

        if type_a not in model_map:
            raise ValueError(f"Unknown item type: {type_a!r}")
        if type_b not in model_map:
            raise ValueError(f"Unknown item type: {type_b!r}")

        container_a = containers[type_a]
        container_b = containers[type_b]

        # Fetch both items (fail fast before any writes)
        raw_a = container_a.get(id_a)
        raw_b = container_b.get(id_b)

        # Deep copy of first item for rollback
        original_a_raw = _json.loads(_json.dumps(raw_a))

        # Load as Pydantic models for validation and format normalization
        item_a = model_map[type_a](**raw_a)
        item_b = model_map[type_b](**raw_b)

        # Link bidirectionally
        field_a, field_b, changed = DatabaseManager.link_items(item_a, type_a, item_b, type_b)

        result = {
            "field_a": field_a,
            "field_b": field_b,
            "stored_id_a": raw_a["id"],
            "stored_id_b": raw_b["id"],
            "changed": changed,
        }

        if not changed:
            return result

        # Upsert first item
        container_a.upsert(item_a.id, data=item_a, run_indexer=False)
        try:
            container_b.upsert(item_b.id, data=item_b, run_indexer=False)
        except Exception as write_err:
            # Rollback first write using the raw original document
            try:
                container_a.client.upsert_item(original_a_raw)
            except Exception as rollback_err:
                raise RuntimeError(
                    f"Failed to write {type_b} '{id_b}' and rollback of {type_a} '{id_a}' also failed: {rollback_err}"
                ) from write_err
            raise

        if run_indexer:
            container_a.run_indexer()
            if type_a != type_b:
                container_b.run_indexer()

        return result

    def save_memory_with_links(
        self,
        *,
        raw_memory: dict,
        guideline_ids: list[str],
        raw_examples: list[dict],
        example_service: Optional[str] = None,
    ) -> dict:
        """Create a Memory with linked Guidelines and Examples, and save all to the database.

        This is the shared implementation behind the mention-agent and
        thread-resolution workflows.  It:

        1. Constructs the Memory, Examples, and fetches Guidelines.
        2. Sets up bidirectional links via ``link_items()``.
        3. Saves all items with application-level rollback: if any write
           fails, all previously successful writes are undone (new items
           are soft-deleted, updated guidelines are restored from snapshots).

        Args:
            raw_memory: Dict of Memory fields (must already include ``source``,
                ``source_comment_id``, ``is_exception``, ``service``, etc.).
            guideline_ids: Guideline IDs (web-format or full URL) to link.
            raw_examples: List of dicts for Example construction (popped from
                the raw_memory by the caller).
            example_service: Value to assign to ``example.service`` for each
                example (``None`` for mention workflow, ``package_name`` for
                thread-resolution exceptions).

        Returns:
            ``{"success": [...], "failures": {...}}``
        """
        import json as _json
        import uuid

        from src._models import Example, Guideline, Memory
        from src._search_manager import SearchManager

        memory = Memory(**raw_memory)
        old_memory_id = memory.id
        memory.id = str(uuid.uuid4())
        memory_id = memory.id

        examples = [Example(**ex) for ex in raw_examples]
        for example in examples:
            example.service = example_service
            example.id = example.id.replace(old_memory_id, memory_id)
            DatabaseManager.link_items(example, "example", memory, "memory")

        guidelines = []
        guideline_snapshots = {}  # DB-format id -> raw dict snapshot for rollback
        for guideline_id in guideline_ids:
            try:
                raw_guideline = self.guidelines.get(guideline_id)
                guideline_snapshots[raw_guideline["id"]] = _json.loads(_json.dumps(raw_guideline))
                guideline = Guideline(**raw_guideline)
                DatabaseManager.link_items(guideline, "guideline", memory, "memory")
                guidelines.append(guideline)
            except CosmosResourceNotFoundError:
                continue
            except Exception as e:
                print(f"Error retrieving guideline {guideline_id}: {e}")
                continue

        # ── Atomic-ish save with rollback ────────────────────────────────
        # Track successful writes so we can undo them on failure.
        # - New items (examples, memory): rollback = soft-delete
        # - Existing items (guidelines): rollback = restore snapshot
        saved_new_items = []       # (container, id) for new items to soft-delete
        saved_guideline_ids = []   # DB-format IDs of guidelines we updated

        def _rollback(error_msg: str):
            """Best-effort undo of all successful writes."""
            for container, item_id in saved_new_items:
                try:
                    container.delete(item_id, run_indexer=False)
                except Exception as rb_err:
                    print(f"Rollback warning: failed to delete {item_id}: {rb_err}")
            for g_db_id in saved_guideline_ids:
                try:
                    self.guidelines.client.upsert_item(guideline_snapshots[g_db_id])
                except Exception as rb_err:
                    print(f"Rollback warning: failed to restore guideline {g_db_id}: {rb_err}")
            print(f"Rolled back {len(saved_new_items)} new item(s) and "
                  f"{len(saved_guideline_ids)} guideline update(s) after error: {error_msg}")

        # 1. Examples (new items)
        for example in examples:
            try:
                self.examples.upsert(example.id, data=example, run_indexer=False)
                saved_new_items.append((self.examples, example.id))
            except Exception as e:
                _rollback(str(e))
                return {"success": [], "failures": {example.id: str(e)}}

        # 2. Memory (new item)
        try:
            self.memories.upsert(memory.id, data=memory, run_indexer=False)
            saved_new_items.append((self.memories, memory.id))
        except Exception as e:
            _rollback(str(e))
            return {"success": [], "failures": {memory.id: str(e)}}

        # 3. Guidelines (updates to existing items)
        for guideline in guidelines:
            db_id = guideline_id_to_db(guideline.id)
            try:
                self.guidelines.upsert(guideline.id, data=guideline, run_indexer=False)
                saved_guideline_ids.append(db_id)  # matches guideline_snapshots keys
            except Exception as e:
                _rollback(str(e))
                return {"success": [], "failures": {guideline.id: str(e)}}

        # All writes succeeded
        SearchManager.run_indexers()
        return {"success": [memory_id], "failures": {}}


class BasicContainer:
    """Basic container client for Azure Cosmos DB operations."""

    def __init__(self, manager: DatabaseManager, container_name: str):
        self.client = manager.database.get_container_client(container_name)
        self.preprocess_id: callable = None
        self.container_name = container_name
        self._environment = manager.environment

    def _to_dict(self, data):
        if BaseModel and isinstance(data, BaseModel):
            if hasattr(data, "model_dump_db"):
                return data.model_dump_db()
            return data.model_dump()
        return dict(data) if not isinstance(data, dict) else data

    def create(self, item_id: str, *, data: Any, run_indexer: bool = True):
        """
        Create a new item. Raises an error if the item already exists.
        """
        if self.preprocess_id:
            # pylint: disable=not-callable
            item_id = self.preprocess_id(item_id)
        data_dict = self._to_dict(data)
        # Remove None values
        data_dict = {k: v for k, v in data_dict.items() if v is not None}
        # Ensure 'id' is set and preprocessed
        if data_dict.get("id") and self.preprocess_id:
            # pylint: disable=not-callable
            data_dict["id"] = self.preprocess_id(data_dict["id"])
        elif not data_dict.get("id"):
            data_dict["id"] = item_id
        # Check if item exists
        try:
            self.client.read_item(item=data_dict["id"], partition_key=data_dict["id"])
            # If no exception, item exists
            return {"status": "error", "message": f"Item with id '{data_dict['id']}' already exists."}
        except CosmosResourceNotFoundError:
            # Item does not exist, safe to create
            self.client.create_item(body=data_dict)
            value = {"status": "created", "id": data_dict["id"]}
            if run_indexer:
                self.run_indexer()
            return value

    def upsert(self, item_id: str, *, data: Any, run_indexer: bool = True):
        """
        Upsert an item. If it exists, update it; if not, create it.
        """
        if self.preprocess_id:
            # pylint: disable=not-callable
            item_id = self.preprocess_id(item_id)
        data_dict = self._to_dict(data)
        # Ensure the id in the payload is also preprocessed
        if "id" in data_dict and self.preprocess_id:
            # pylint: disable=not-callable
            data_dict["id"] = self.preprocess_id(data_dict["id"])
        value = self.client.upsert_item({"id": item_id, **data_dict})
        if run_indexer:
            self.run_indexer()
        return value

    def get(self, item_id: str):
        """
        Get an item by its ID. If preprocess_id is provided, it will be applied to the item_id before fetching.
        """
        if self.preprocess_id:
            # pylint: disable=not-callable
            item_id = self.preprocess_id(item_id)
        return self.client.read_item(item=item_id, partition_key=item_id)

    def get_batched(self, item_ids: list[str], *, batch_size: int = 50) -> list[dict]:
        """Get multiple items by ID using batched cross-partition queries."""
        if not item_ids:
            return []

        if self.preprocess_id:
            # pylint: disable=not-callable
            item_ids = [self.preprocess_id(item_id) for item_id in item_ids]

        unique_ids = list(dict.fromkeys(item_ids))
        results = []

        for start in range(0, len(unique_ids), batch_size):
            batch = unique_ids[start : start + batch_size]
            placeholders = ",".join(f"@id{i}" for i in range(len(batch)))
            query = f"SELECT * FROM c WHERE c.id IN ({placeholders})"
            parameters = [{"name": f"@id{i}", "value": value} for i, value in enumerate(batch)]
            results.extend(
                list(
                    self.client.query_items(
                        query=query,
                        parameters=parameters,
                        enable_cross_partition_query=True,
                    )
                )
            )

        return results

    def delete(self, item_id: str, *, run_indexer: bool = True):
        """
        Soft-delete an item by its ID. Sets 'isDeleted' to True instead of removing the document.
        """
        if self.preprocess_id:
            # pylint: disable=not-callable
            item_id = self.preprocess_id(item_id)
        item = self.get(item_id)
        item["isDeleted"] = True
        value = self.client.upsert_item(item)

        if run_indexer:
            self.run_indexer()
        return value

    def run_indexer(self):
        """
        Trigger the Azure Search indexer for this container (examples, guidelines, or memories).
        """
        settings = SettingsManager(environment=self._environment)
        indexer_name = f"{self.container_name}-indexer"
        search_endpoint = settings.get("SEARCH_ENDPOINT")
        client = SearchIndexerClient(endpoint=search_endpoint, credential=get_credential())
        status = client.get_indexer_status(indexer_name)
        if status.status in ["inProgress"]:
            return
        client.run_indexer(indexer_name)


class GuidelinesContainer(BasicContainer):
    """Container client for guidelines operations."""

    def __init__(self, manager: DatabaseManager, container_name: str):
        super().__init__(manager, container_name)
        self.preprocess_id = guideline_id_to_db


class ReviewJobsContainer(BasicContainer):
    """Container client for review jobs operations."""

    def cleanup_old_jobs(self, retention_seconds):
        """
        Clean up old review jobs that have a 'finished' timestamp older than the specified retention period
        """
        now = time.time()
        query = (
            f"SELECT c.id, c.finished FROM c WHERE IS_DEFINED(c.finished) AND c.finished < {now - retention_seconds}"
        )
        for item in self.client.query_items(query=query, enable_cross_partition_query=True):
            self.delete(item["id"])

    def run_indexer(self):
        # reviews_jobs container does not have an indexer
        pass


class MetricsContainer(BasicContainer):
    """Container client for metrics operations."""

    def run_indexer(self):
        # metrics container does not have an indexer
        pass


class EvalsContainer(BasicContainer):
    """Container client for evaluations operations."""

    def run_indexer(self):
        # evaluations container does not have an indexer
        pass
