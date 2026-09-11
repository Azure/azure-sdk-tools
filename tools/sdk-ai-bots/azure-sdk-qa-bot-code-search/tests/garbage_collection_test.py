from __future__ import annotations

from datetime import datetime, timedelta, timezone

from code_search.garbage_collection import _clean_active_repository


class _SearchClient:
    def __init__(self) -> None:
        self.filters: list[str] = []

    def search(self, **kwargs):
        self.filters.append(kwargs["filter"])
        return []


class _Store:
    def __init__(self) -> None:
        self.deleted: list[tuple[int, bool]] = []
        self.staging_deleted: list[int] = []

    def delete_generation_artifacts(
        self, repository_key: str, generation: int, *, delete_manifest: bool
    ) -> None:
        self.deleted.append((generation, delete_manifest))

    def delete_staging_record(self, repository_key: str, generation: int) -> None:
        self.staging_deleted.append(generation)


def test_cleanup_keeps_active_and_two_previous_generations(tmp_path) -> None:
    indexed_at = (datetime.now(timezone.utc) - timedelta(days=8)).isoformat()
    manifests = {
        generation: {"indexed_at": indexed_at} for generation in range(1, 6)
    }
    client = _SearchClient()
    store = _Store()

    _clean_active_repository(
        client,  # type: ignore[arg-type]
        store,  # type: ignore[arg-type]
        tmp_path,
        "repository",
        {
            "git_url": "https://github.com/Azure/example.git",
            "git_ref": "refs/heads/main",
            "active_generation": 5,
        },
        manifests,
        {5: {"created_at": indexed_at}},
    )

    assert store.deleted == [(1, False), (2, False)]
    assert store.staging_deleted == [5]
    assert "valid_to_generation le 3" in client.filters[0]
