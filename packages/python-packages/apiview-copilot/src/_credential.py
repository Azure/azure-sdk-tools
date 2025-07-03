import os

from azure.identity import DefaultAzureCredential, AzurePipelinesCredential


def in_ci():
    return os.getenv("TF_BUILD", None) and "tests" in os.getenv("SYSTEM_DEFINITIONNAME", "")


def get_credential():
    if in_ci():
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
