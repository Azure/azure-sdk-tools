"""Shared Azure Blob Storage helpers for dataset preparation.

Both ``curate`` (all blobs) and ``online_snapshot`` (recent rolling window) pull the
same Q&A markdown container, and the evaluation entry point needs the same credential
selection. This module centralizes credential choice and the blob-download loop so the
logic lives in exactly one place.
"""

from __future__ import annotations

import logging
import os
import re
from datetime import datetime
from pathlib import Path
from typing import Any

# Date stamps embedded in blob filenames, e.g. ``typespec_2026_06_18.md`` / ``apispec20260612.md``.
_DATE_PATTERNS = (r"(\d{4})(\d{2})(\d{2})", r"(\d{4})_(\d{2})_(\d{2})")


def extract_date(filename: str) -> datetime | None:
    """Best-effort parse of a ``YYYYMMDD`` / ``YYYY_MM_DD`` date out of a filename."""
    for pattern in _DATE_PATTERNS:
        match = re.search(pattern, filename)
        if match:
            try:
                year, month, day = map(int, match.groups())
                return datetime(year, month, day)
            except ValueError:
                continue
    return None


def credential_for(is_ci: bool) -> Any:
    """Select an Azure credential: ``az login`` locally, pipeline identity in CI."""
    from azure.identity import (
        AzureCliCredential,
        AzurePipelinesCredential,
        DefaultAzureCredential,
    )

    if not is_ci:
        return AzureCliCredential()
    sc_id = os.getenv("AZURESUBSCRIPTION_SERVICE_CONNECTION_ID")
    client_id = os.getenv("AZURESUBSCRIPTION_CLIENT_ID")
    tenant_id = os.getenv("AZURESUBSCRIPTION_TENANT_ID")
    token = os.getenv("SYSTEM_ACCESSTOKEN")
    if all([sc_id, client_id, tenant_id, token]):
        return AzurePipelinesCredential(
            service_connection_id=sc_id,  # type: ignore[arg-type]
            client_id=client_id,  # type: ignore[arg-type]
            tenant_id=tenant_id,  # type: ignore[arg-type]
            system_access_token=token,  # type: ignore[arg-type]
        )
    logging.warning("AZURESUBSCRIPTION_*/SYSTEM_ACCESSTOKEN missing; using DefaultAzureCredential.")
    return DefaultAzureCredential()


def get_md_container_client(credential: Any) -> Any:
    """Container client for the configured Q&A markdown storage container."""
    from azure.storage.blob import BlobServiceClient

    account = os.environ["STORAGE_BLOB_ACCOUNT"]
    container = os.environ["AI_ONLINE_PERFORMANCE_EVALUATION_STORAGE_CONTAINER"]
    service = BlobServiceClient(account_url=f"https://{account}.blob.core.windows.net", credential=credential)
    return service.get_container_client(container)


def download_md_blobs(dest: Path, credential: Any, *, since: datetime | None = None) -> int:
    """Download ``.md`` blobs into ``dest``; return the number downloaded.

    When ``since`` is given, only blobs whose filename-stamped date is on/after it are
    pulled (the online rolling window); otherwise every ``.md`` blob is downloaded.
    """
    container_client = get_md_container_client(credential)
    dest.mkdir(parents=True, exist_ok=True)
    count = 0
    for item in container_client.list_blobs():
        filename = re.split(r"[\\/]", item.name)[-1]
        if not filename.endswith(".md"):
            continue
        if since is not None:
            file_date = extract_date(filename)
            if file_date is None or file_date < since:
                continue
        logging.info("download %s", item.name)
        blob_client = container_client.get_blob_client(item.name)
        (dest / filename).write_bytes(blob_client.download_blob().readall())
        count += 1
    return count
