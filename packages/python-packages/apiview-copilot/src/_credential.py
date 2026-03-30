# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Module for retrieving Azure credentials."""

import logging
import os
import threading

from azure.identity import (
    AzureCliCredential,
    AzureDeveloperCliCredential,
    AzurePipelinesCredential,
    ChainedTokenCredential,
    ManagedIdentityCredential,
)

logger = logging.getLogger(__name__)

_credential_cache = {"instance": None}
_credential_lock = threading.Lock()


def in_ci():
    """Check if the code is running in a CI environment."""
    return os.getenv("TF_BUILD", None) and "tests" in os.getenv("SYSTEM_DEFINITIONNAME", "")


def get_credential():
    """Get a shared Azure credential instance.

    Returns a cached singleton so that concurrent threads reuse the same
    credential instead of each spawning their own token-acquisition
    subprocesses (which fails under high parallelism).
    """
    if _credential_cache["instance"] is not None:
        return _credential_cache["instance"]

    with _credential_lock:
        if _credential_cache["instance"] is not None:
            return _credential_cache["instance"]

        if in_ci():
            service_connection_id = os.environ["AZURESUBSCRIPTION_SERVICE_CONNECTION_ID"]
            client_id = os.environ["AZURESUBSCRIPTION_CLIENT_ID"]
            tenant_id = os.environ["AZURESUBSCRIPTION_TENANT_ID"]
            system_access_token = os.environ["SYSTEM_ACCESSTOKEN"]
            _credential_cache["instance"] = AzurePipelinesCredential(
                service_connection_id=service_connection_id,
                client_id=client_id,
                tenant_id=tenant_id,
                system_access_token=system_access_token,
            )
        else:
            _credential_cache["instance"] = ChainedTokenCredential(
                ManagedIdentityCredential(),
                AzureCliCredential(),
                AzureDeveloperCliCredential(),
            )

        return _credential_cache["instance"]


def warm_up_credential():
    """Pre-acquire a token so it is cached before parallel workers start.

    This prevents a thundering-herd of concurrent subprocess calls to
    ``az account get-access-token`` that fail under high parallelism.
    """
    credential = get_credential()
    try:
        credential.get_token("https://cognitiveservices.azure.com/.default")
    except Exception as exc:
        logger.warning("Credential warm-up failed: %s", exc)
