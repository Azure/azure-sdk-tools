from dotenv import load_dotenv
import os
import zipfile
import subprocess
from typing import Optional

load_dotenv(override=True)

RESOURCE_GROUP = os.getenv("AZURE_RESOURCE_GROUP")
SUBSCRIPTION_ID = os.getenv("AZURE_SUBSCRIPTION_ID")
APP_NAME = os.getenv("AZURE_APP_NAME")


def _zip_current_repo(output_filename: str):
    """Zip the current repository."""
    print("Zipping the current repository...")
    folders_to_keep = ["src", "guidelines", "prompts", "metadata"]
    files_to_keep = ["app.py", "requirements.txt", "startup.txt"]
    with zipfile.ZipFile(output_filename, "w", zipfile.ZIP_DEFLATED) as zip_file:
        for root, _, files in os.walk("."):
            for file in files:
                file_path = os.path.join(root, file)
                rel_path = os.path.relpath(file_path, ".")
                # get the top_level folder name
                top_level_folder = rel_path.split(os.sep)[0]
                if not top_level_folder in folders_to_keep and rel_path not in files_to_keep:
                    continue
                zip_file.write(file_path, rel_path)
    print(f"Repository zipped to {output_filename}")


def deploy_app_to_azure(
    app_name: Optional[str] = None,
    resource_group: Optional[str] = None,
    subscription_id: Optional[str] = None,
):
    """Deploy the zipped repository to Azure App Service using Azure CLI."""
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
        raise ValueError(f"Missing required environment variables: {', '.join(missing_vars)}")

    zip_file = "repo.zip"
    _zip_current_repo(zip_file)

    try:
        print("Deploying to Azure App Service...")
        cmd = [
            "az",
            "webapp",
            "deploy",
            "--resource-group",
            resource_group,
            "--name",
            app_name,
            "--src-path",
            zip_file,
            "--subscription",
            subscription_id,
            "--type",
            "zip",
        ]
        print("Running command:", " ".join(cmd))
        subprocess.run(cmd, check=True)
        print("Deployment completed successfully.")
    except subprocess.CalledProcessError as e:
        print(f"An error occurred during deployment: {e}")
        raise
    finally:
        if os.path.exists(zip_file):
            os.remove(zip_file)
            print(f"Temporary zip file {zip_file} removed.")

    # After deployment, set the startup command from startup.txt
    startup_file = "startup.txt"
    if os.path.exists(startup_file):
        with open(startup_file, "r") as f:
            startup_command = f.read().strip()
        print(f"Setting Azure App Service startup command to: {startup_command}")
        cmd = [
            "az",
            "webapp",
            "config",
            "set",
            "--resource-group",
            resource_group,
            "--name",
            app_name,
            "--startup-file",
            startup_command,
            "--subscription",
            subscription_id,
        ]
        print("Running command:", " ".join(cmd))
        subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


if __name__ == "__main__":
    deploy_app_to_azure()
