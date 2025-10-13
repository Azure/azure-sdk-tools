# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""Module for retrieving Azure credentials."""

import os

from azure.identity import AzurePipelinesCredential, DefaultAzureCredential


def in_ci():
    """Check if the code is running in a CI environment."""
    return os.getenv("TF_BUILD", None) and "tests" in os.getenv("SYSTEM_DEFINITIONNAME", "")


def get_credential():
    """Get Azure credentials based on the environment."""
    if in_ci():
        # These are used by Azure Pipelines and should not be changed
        service_connection_id = os.environ["AZURESUBSCRIPTION_SERVICE_CONNECTION_ID"]
        client_id = os.environ["AZURESUBSCRIPTION_CLIENT_ID"]
        tenant_id = os.environ["AZURESUBSCRIPTION_TENANT_ID"]
        system_access_token = os.environ["SYSTEM_ACCESSTOKEN"]
        return AzurePipelinesCredential(
            service_connection_id=service_connection_id,
            client_id=client_id,
            tenant_id=tenant_id,
            system_access_token=system_access_token,
        )

    return DefaultAzureCredential()
