from dotenv import load_dotenv
import os
from typing import Optional
import zipfile
from azure.identity import DefaultAzureCredential
from azure.mgmt.web import WebSiteManagementClient

load_dotenv(override=True)

CREDENTIAL = DefaultAzureCredential()
RESOURCE_GROUP = os.getenv("AZURE_RESOURCE_GROUP")
SUBSCRIPTION_ID = os.getenv("AZURE_SUBSCRIPTION_ID")
APP_NAME = os.getenv("AZURE_APP_NAME")


def _zip_current_repo(output_filename: str):
    """Zip the current repository."""
    print("Zipping the current repository...")
    with zipfile.ZipFile(output_filename, "w", zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk("."):
            for file in files:
                file_path = os.path.join(root, file)
                arcname = os.path.relpath(file_path, ".")
                zipf.write(file_path, arcname)
    print(f"Repository zipped to {output_filename}")


def deploy_to_azure(
    app_name: Optional[str] = None,
    resource_group: Optional[str] = None,
    subscription_id: Optional[str] = None,
):
    """Deploy the zipped repository to Azure App Service."""
    app_name = app_name or APP_NAME
    resource_group = resource_group or RESOURCE_GROUP
    subscription_id = subscription_id or SUBSCRIPTION_ID
    missing_vars = []

    if not app_name:
        missing_vars.append("AZURE_APP_NAME")
    if not resource_group:
        missing_vars.append("AZURE_RESOURCE_GROUP")
    if not subscription_id:
        missing_vars.append("AZURE_SUBSCRIPTION_ID")
    if missing_vars:
        raise ValueError(f"Missing environment variables: {', '.join(missing_vars)}")
    zip_file = "repo.zip"
    try:
        # Zip the current repository
        _zip_current_repo(zip_file)

        # Authenticate using DefaultAzureCredential
        client = WebSiteManagementClient(CREDENTIAL, subscription_id)

        # Deploy the zip file
        print(f"Deploying {zip_file} to Azure App Service '{app_name}'...")
        with open(zip_file, "rb") as file_data:
            client.web_apps.begin_create_or_update_zip_deployment(
                resource_group_name=resource_group, name=app_name, zip_file=file_data
            ).result()
        print("Deployment successful!")
    except Exception as e:
        print(f"Deployment failed: {e}")
    finally:
        # Clean up
        if os.path.exists(zip_file):
            os.remove(zip_file)
            print(f"Cleaned up temporary file: {zip_file}")
