from __future__ import annotations

import logging
import os
import shutil
import stat
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from uuid import uuid4

from azure.identity.aio import DefaultAzureCredential
from azure.storage.blob.aio import BlobServiceClient

from . import config
from .git import checkout_repository
from .models import RepositoryConfig, SelectedFile
from .repositories import aggregate_repository_configs
from .selection import select_source_files
from .storage import BlobRepositoryStore
from .tenant_source import load_tenant_repository_configs

logger = logging.getLogger(__name__)


async def sync_repositories() -> None:
    credential = DefaultAzureCredential()
    try:
        await config.load(credential)
        settings = config.settings()
        repositories = aggregate_repository_configs(
            load_tenant_repository_configs()
        )
        if not repositories:
            raise RuntimeError("TenantConfig declares no code repositories")

        run_root = _create_run_root(settings.work_root)
        try:
            files, repository_manifests = _prepare_repositories(
                repositories,
                run_root,
                settings.max_file_bytes,
            )
            manifest = {
                "schema_version": 1,
                "updated_at": datetime.now(timezone.utc)
                .isoformat()
                .replace("+00:00", "Z"),
                "files": [file.path for file in files],
                "repositories": repository_manifests,
            }
            async with BlobServiceClient(
                account_url=settings.storage_endpoint,
                credential=credential,
            ) as service:
                store = BlobRepositoryStore(
                    service.get_container_client(settings.storage_container),
                    prefix=settings.blob_prefix,
                    concurrency=settings.storage_concurrency,
                )
                await store.reconcile(files, manifest)
        finally:
            _remove_run_root(run_root, settings.work_root)
    finally:
        await credential.close()


def _prepare_repositories(
    repositories: list[RepositoryConfig],
    run_root: Path,
    max_file_bytes: int,
) -> tuple[list[SelectedFile], list[dict[str, object]]]:
    files_by_path: dict[str, SelectedFile] = {}
    manifests: list[dict[str, object]] = []

    for repository in repositories:
        destination = run_root / repository.owner / repository.repository
        logger.info("checking out %s %s", repository.git_url, repository.git_ref)
        snapshot = checkout_repository(repository, destination)
        selected = select_source_files(
            snapshot.root,
            repository,
            excluded_roots=tuple(item.path for item in snapshot.submodules),
            max_file_bytes=max_file_bytes,
        )

        submodule_paths = tuple(item.path for item in snapshot.submodules)
        for submodule in snapshot.submodules:
            descendants = tuple(
                path[len(submodule.path) + 1 :]
                for path in submodule_paths
                if path.startswith(f"{submodule.path}/")
            )
            selected.extend(
                select_source_files(
                    snapshot.root
                    / Path(*PurePosixPath(submodule.path).parts),
                    repository,
                    output_prefix=submodule.path,
                    excluded_roots=descendants,
                    max_file_bytes=max_file_bytes,
                )
            )

        if not selected:
            raise RuntimeError(
                f"repository selection produced no files for {repository.namespace}"
            )
        for file in selected:
            blob_path = f"{repository.namespace}/{file.path}"
            if blob_path in files_by_path:
                raise RuntimeError(f"duplicate repository blob path: {blob_path}")
            files_by_path[blob_path] = SelectedFile(
                path=blob_path,
                content=file.content,
            )

        manifests.append(
            {
                "name": repository.namespace,
                "git_url": repository.git_url,
                "git_ref": repository.git_ref,
                "commit_sha": snapshot.commit,
                "submodules": [
                    submodule.to_manifest()
                    for submodule in snapshot.submodules
                ],
            }
        )
        logger.info(
            "selected %d files from %s at %s",
            len(selected),
            repository.namespace,
            snapshot.commit,
        )

    return (
        sorted(files_by_path.values(), key=lambda item: item.path),
        sorted(manifests, key=lambda item: str(item["name"]).casefold()),
    )


def _create_run_root(work_root: Path) -> Path:
    work_root.mkdir(parents=True, exist_ok=True)
    run_root = work_root / f"run-{uuid4().hex}"
    run_root.mkdir()
    return run_root


def _remove_run_root(run_root: Path, work_root: Path) -> None:
    shutil.rmtree(run_root, onexc=_remove_readonly)
    try:
        work_root.rmdir()
    except OSError:
        pass


def _remove_readonly(function: object, path: str, error: BaseException) -> None:
    if not isinstance(error, PermissionError):
        raise error
    os.chmod(path, stat.S_IWRITE)
    if not callable(function):
        raise TypeError("cleanup callback is not callable")
    function(path)
