from __future__ import annotations

import hashlib
import json
import shutil
import zipfile
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from azure.core import MatchConditions
from azure.core.credentials import TokenCredential
from azure.core.exceptions import ResourceExistsError, ResourceNotFoundError
from azure.storage.blob import BlobServiceClient, ContentSettings

from . import CODE_INDEX_SCHEMA_VERSION

CATALOG_BLOB = "catalog.json"


@dataclass(frozen=True, slots=True)
class CatalogSnapshot:
    value: dict[str, Any]
    etag: str | None


class BlobCatalogStore:
    def __init__(
        self, endpoint: str, container: str, credential: TokenCredential
    ) -> None:
        self._service = BlobServiceClient(endpoint, credential=credential)
        self._container = self._service.get_container_client(container)
        try:
            self._container.create_container()
        except ResourceExistsError:
            pass

    def close(self) -> None:
        self._service.close()

    def load_catalog(self) -> CatalogSnapshot:
        blob = self._container.get_blob_client(CATALOG_BLOB)
        try:
            download = blob.download_blob()
            data = download.readall()
        except ResourceNotFoundError:
            return CatalogSnapshot(
                {
                    "schema_version": CODE_INDEX_SCHEMA_VERSION,
                    "tenants": {},
                    "repositories": [],
                },
                None,
            )
        try:
            value = json.loads(data.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise RuntimeError(f"{CATALOG_BLOB} is not valid UTF-8 JSON") from exc
        return CatalogSnapshot(value, download.properties.etag)

    def publish_catalog(
        self, catalog: dict[str, Any], expected_etag: str | None
    ) -> None:
        payload = _json_bytes(catalog)
        blob = self._container.get_blob_client(CATALOG_BLOB)
        if expected_etag is None:
            blob.upload_blob(
                payload,
                overwrite=False,
                content_settings=ContentSettings(content_type="application/json"),
            )
            return
        blob.upload_blob(
            payload,
            overwrite=True,
            etag=expected_etag,
            match_condition=MatchConditions.IfNotModified,
            content_settings=ContentSettings(content_type="application/json"),
        )

    def upload_manifest(
        self, repository_key: str, generation: int, manifest: dict[str, Any]
    ) -> tuple[str, str, dict[str, Any]]:
        name = f"manifests/{repository_key}/{generation}.json"
        blob = self._container.get_blob_client(name)
        try:
            blob.upload_blob(
                _json_bytes(manifest),
                overwrite=False,
                content_settings=ContentSettings(content_type="application/json"),
            )
            persisted = manifest
        except ResourceExistsError:
            persisted = self.read_manifest(name)
            if (
                persisted.get("resolved_commit_sha")
                != manifest.get("resolved_commit_sha")
                or persisted.get("index_identity") != manifest.get("index_identity")
            ):
                raise RuntimeError(
                    f"immutable generation manifest conflicts with retry: {name}"
                )
        return name, blob.url, persisted

    def try_read_manifest(
        self, repository_key: str, generation: int
    ) -> tuple[str, str, dict[str, Any]] | None:
        name = f"manifests/{repository_key}/{generation}.json"
        blob = self._container.get_blob_client(name)
        try:
            data = blob.download_blob().readall()
        except ResourceNotFoundError:
            return None
        return name, blob.url, json.loads(data.decode("utf-8"))

    def reserve_generation(
        self,
        repository_key: str,
        generation: int,
        build_identity: str,
        resolved_commit_sha: str,
        git_url: str,
        git_ref: str,
    ) -> None:
        name = f"staging/{repository_key}/{generation}.json"
        reservation = {
            "build_identity": build_identity,
            "resolved_commit_sha": resolved_commit_sha,
            "git_url": git_url,
            "git_ref": git_ref,
            "created_at": datetime.now(timezone.utc).isoformat(),
        }
        blob = self._container.get_blob_client(name)
        try:
            blob.upload_blob(
                _json_bytes(reservation),
                overwrite=False,
                content_settings=ContentSettings(content_type="application/json"),
            )
            return
        except ResourceExistsError:
            existing = json.loads(blob.download_blob().readall().decode("utf-8"))
        if any(
            existing.get(key) != reservation[key]
            for key in ("build_identity", "resolved_commit_sha", "git_url", "git_ref")
        ):
            raise RuntimeError(
                f"generation {generation} for {repository_key} is already staged "
                "for a different commit or index identity"
            )

    def list_repository_keys(self) -> set[str]:
        keys: set[str] = set()
        for prefix in ("manifests/", "staging/", "removals/", "states/"):
            for blob in self._container.list_blobs(name_starts_with=prefix):
                parts = blob.name.split("/")
                if len(parts) >= 2 and parts[1]:
                    keys.add(parts[1])
        return keys

    def list_generation_records(
        self, category: str, repository_key: str
    ) -> dict[int, dict[str, Any]]:
        prefix = f"{category}/{repository_key}/"
        records: dict[int, dict[str, Any]] = {}
        for blob in self._container.list_blobs(name_starts_with=prefix):
            name = blob.name.removeprefix(prefix).removesuffix(".json")
            if not name.isdigit():
                continue
            records[int(name)] = json.loads(
                self._container.get_blob_client(blob.name)
                .download_blob()
                .readall()
                .decode("utf-8")
            )
        return records

    def delete_generation_artifacts(
        self,
        repository_key: str,
        generation: int,
        *,
        delete_manifest: bool,
    ) -> None:
        names = [
            f"states/{repository_key}/{generation}.zip",
            f"staging/{repository_key}/{generation}.json",
        ]
        if delete_manifest:
            names.append(f"manifests/{repository_key}/{generation}.json")
        self._delete_blobs(names)

    def delete_staging_record(self, repository_key: str, generation: int) -> None:
        self._delete_blobs([f"staging/{repository_key}/{generation}.json"])

    def removal_marker(
        self,
        repository_key: str,
        git_url: str,
        git_ref: str,
    ) -> dict[str, Any]:
        blob = self._container.get_blob_client(f"removals/{repository_key}.json")
        try:
            return json.loads(blob.download_blob().readall().decode("utf-8"))
        except ResourceNotFoundError:
            value = {
                "git_url": git_url,
                "git_ref": git_ref,
                "removed_at": datetime.now(timezone.utc).isoformat(),
            }
            try:
                blob.upload_blob(
                    _json_bytes(value),
                    overwrite=False,
                    content_settings=ContentSettings(content_type="application/json"),
                )
                return value
            except ResourceExistsError:
                return json.loads(blob.download_blob().readall().decode("utf-8"))

    def clear_removal_marker(self, repository_key: str) -> None:
        self._delete_blobs([f"removals/{repository_key}.json"])

    def delete_repository_artifacts(self, repository_key: str) -> None:
        names = [
            blob.name
            for prefix in (
                f"manifests/{repository_key}/",
                f"staging/{repository_key}/",
                f"states/{repository_key}/",
                f"removals/{repository_key}",
            )
            for blob in self._container.list_blobs(name_starts_with=prefix)
        ]
        self._delete_blobs(names)

    def _delete_blobs(self, names: list[str]) -> None:
        for name in names:
            try:
                self._container.delete_blob(name, delete_snapshots="include")
            except ResourceNotFoundError:
                pass

    def read_manifest(self, blob_name: str) -> dict[str, Any]:
        blob = self._container.get_blob_client(blob_name)
        try:
            data = blob.download_blob().readall()
        except ResourceNotFoundError as exc:
            raise RuntimeError(f"active generation manifest is missing: {blob_name}") from exc
        return json.loads(data.decode("utf-8"))

    def upload_state_archive(
        self,
        repository_key: str,
        generation: int,
        archive: Path,
        checksum: str,
    ) -> str:
        blob = self._container.get_blob_client(
            f"states/{repository_key}/{generation}.zip"
        )
        try:
            with archive.open("rb") as stream:
                blob.upload_blob(
                    stream,
                    overwrite=False,
                    metadata={"sha256": checksum},
                    content_settings=ContentSettings(content_type="application/zip"),
                )
            return checksum
        except ResourceExistsError:
            properties = blob.get_blob_properties()
            persisted = (properties.metadata or {}).get("sha256")
            if not persisted:
                persisted = hashlib.sha256(blob.download_blob().readall()).hexdigest()
            if persisted != checksum:
                raise RuntimeError(
                    f"immutable CocoIndex state conflicts with retry for "
                    f"{repository_key} generation {generation}"
                )
            return persisted

    def download_state_archive(
        self,
        repository_key: str,
        generation: int,
        destination: Path,
        expected_checksum: str,
    ) -> None:
        blob = self._container.get_blob_client(
            f"states/{repository_key}/{generation}.zip"
        )
        try:
            with destination.open("wb") as stream:
                blob.download_blob().readinto(stream)
        except ResourceNotFoundError as exc:
            destination.unlink(missing_ok=True)
            raise RuntimeError(
                f"CocoIndex state archive is missing for {repository_key} "
                f"generation {generation}"
            ) from exc
        if expected_checksum:
            actual = hashlib.sha256(destination.read_bytes()).hexdigest()
            if actual != expected_checksum:
                destination.unlink(missing_ok=True)
                raise RuntimeError(
                    f"CocoIndex state checksum mismatch for {repository_key} "
                    f"generation {generation}"
                )


class StateCache:
    def __init__(self, work_root: Path, repository_key: str) -> None:
        self._work_root = work_root
        self._repository_key = repository_key
        self.db_path = work_root / "states" / repository_key / "db"
        self._metadata_path = work_root / "states" / repository_key / "state.json"
        self._build_identity = ""
        self.prior_documents_added = 0
        self.prior_documents_updated = 0
        self.prior_documents_closed = 0

    def prepare(
        self,
        store: BlobCatalogStore,
        active_generation: int,
        prospective_generation: int,
        build_identity: str,
        active_state_checksum: str,
    ) -> None:
        metadata = self._read_metadata()
        active_baseline = (
            metadata.get("generation") == active_generation
            and metadata.get("status") == "active"
        )
        same_retry = (
            metadata.get("generation") == prospective_generation
            and metadata.get("status") in ("building", "staged")
            and metadata.get("build_identity") == build_identity
        )
        reusable = self.db_path.exists() and (active_baseline or same_retry)
        if same_retry:
            self.prior_documents_added = int(metadata.get("documents_added", 0))
            self.prior_documents_updated = int(metadata.get("documents_updated", 0))
            self.prior_documents_closed = int(metadata.get("documents_closed", 0))
        if not reusable:
            shutil.rmtree(self.db_path.parent, ignore_errors=True)
            self.db_path.mkdir(parents=True)
            if active_generation:
                archive = self._archive_path(active_generation)
                archive.parent.mkdir(parents=True, exist_ok=True)
                store.download_state_archive(
                    self._repository_key,
                    active_generation,
                    archive,
                    active_state_checksum,
                )
                _extract_archive(archive, self.db_path)
                archive.unlink(missing_ok=True)
        else:
            self.db_path.mkdir(parents=True, exist_ok=True)
        self._build_identity = build_identity
        self._write_metadata(prospective_generation, "building")

    def mark_staged(
        self, generation: int, documents_added: int, documents_updated: int, documents_closed: int
    ) -> None:
        self.prior_documents_added = documents_added
        self.prior_documents_updated = documents_updated
        self.prior_documents_closed = documents_closed
        self._write_metadata(generation, "staged")

    def mark_active(self, generation: int) -> None:
        self._write_metadata(generation, "active")

    def archive(self, generation: int) -> tuple[Path, str]:
        archive = self._archive_path(generation)
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.unlink(missing_ok=True)
        with zipfile.ZipFile(
            archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6
        ) as output:
            for path in sorted(self.db_path.rglob("*")):
                if path.is_file():
                    output.write(path, path.relative_to(self.db_path).as_posix())
        checksum = hashlib.sha256(archive.read_bytes()).hexdigest()
        return archive, checksum

    def _archive_path(self, generation: int) -> Path:
        return (
            self._work_root
            / "archives"
            / f"{self._repository_key}-{generation}.zip"
        )

    def _read_metadata(self) -> dict[str, Any]:
        if not self._metadata_path.is_file():
            return {}
        try:
            return json.loads(self._metadata_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise RuntimeError(
                f"invalid local state metadata: {self._metadata_path}"
            ) from exc

    def _write_metadata(self, generation: int, status: str) -> None:
        self._metadata_path.parent.mkdir(parents=True, exist_ok=True)
        temporary = self._metadata_path.with_suffix(".tmp")
        temporary.write_text(
            json.dumps(
                {
                    "generation": generation,
                    "status": status,
                    "build_identity": self._build_identity,
                    "documents_added": self.prior_documents_added,
                    "documents_updated": self.prior_documents_updated,
                    "documents_closed": self.prior_documents_closed,
                }
            ),
            encoding="utf-8",
        )
        temporary.replace(self._metadata_path)


def _extract_archive(archive: Path, destination: Path) -> None:
    destination = destination.resolve()
    with zipfile.ZipFile(archive) as source:
        for item in source.infolist():
            target = (destination / item.filename).resolve()
            if destination not in target.parents and target != destination:
                raise RuntimeError(f"invalid path in CocoIndex state archive: {item.filename}")
        source.extractall(destination)


def _json_bytes(value: dict[str, Any]) -> bytes:
    return json.dumps(
        value, ensure_ascii=False, indent=2, sort_keys=True
    ).encode("utf-8")
