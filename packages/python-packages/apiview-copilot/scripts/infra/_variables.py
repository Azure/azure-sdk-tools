import os

import dotenv
from yaml import safe_load

dotenv.load_dotenv(override=True)


# List of variables that require the '-staging' suffix in the staging environment
VARIABLES_WITH_STAGING_SUFFIX = {
    "APP_CONFIGURATION_NAME",
    "APP_SERVICE_PLAN_NAME",
    "COGNITIVE_SERVICES_NAME",
    "COSMOS_ACCOUNT_NAME",
    "KEYVAULT_NAME",
    "RG_NAME",
    "SEARCH_INDEX_NAME",
    "SEARCH_NAME",
    "WEBAPP_NAME",
}

# List of variables that are the same in staging and production and will not
# add a '-staging' suffix
VARIABLES_SHARED = {
    "AI_RG",
    "COSMOS_DB_NAME",
    "EVALS_RG",
    "EVALS_SUBSCRIPTION",
    "EVALS_PROJECT_NAME",
    "FOUNDRY_ACCOUNT_NAME",
    "FOUNDRY_API_VERSION",
    "FOUNDRY_KERNEL_MODEL",
    "FOUNDRY_PROJECT_NAME",
    "GITHUB_APP_KEYVAULT_URL",
    "GITHUB_APP_KEY_NAME",
    "GITHUB_APP_ID",
    "OPENAI_EMBEDDING_DIMENSIONS",
    "OPENAI_EMBEDDING_MODEL",
    "OPENAI_NAME",
    "RG_LOCATION",
    "SUBSCRIPTION_ID",
    "TENANT_ID",
    "VECTORIZER_PROFILE_NAME",
}


class Variables:

    def __init__(self, *, is_staging: bool, path: str = "variables.yaml"):
        data = {}
        try:
            with open(path, "r", encoding="utf-8") as stream:
                data = safe_load(stream)
        except FileNotFoundError as exc:
            raise FileNotFoundError(f"Configuration file not found: {path}") from exc

        missing = []
        for var in VARIABLES_SHARED.union(VARIABLES_WITH_STAGING_SUFFIX):
            val = data.get(var)
            if val is None:
                missing.append(var)
            elif var in VARIABLES_WITH_STAGING_SUFFIX and is_staging:
                val = f"{val}-staging"
            setattr(self, var.lower(), val)
        if missing:
            raise ValueError(f"Missing required variables: {', '.join(missing)}")
        self.search_endpoint = f"https://{self.search_name}.search.windows.net/"
        self.cosmos_endpoint = f"https://{self.cosmos_account_name}.documents.azure.com:443/"
        self.cognitive_services_id = f"/subscriptions/{self.subscription_id}/resourceGroups/{self.rg_name}/providers/Microsoft.CognitiveServices/accounts/{self.cognitive_services_name}"
        self.openai_endpoint = f"https://{self.openai_name}.openai.azure.com/"
        self.openai_id = f"/subscriptions/{self.subscription_id}/resourceGroups/{self.ai_rg}/providers/Microsoft.CognitiveServices/accounts/{self.openai_name}"
        self.keyvault_endpoint = f"https://{self.keyvault_name}.vault.azure.net/"
        self.app_configuration_endpoint = f"https://{self.app_configuration_name}.azconfig.io"
        self.webapp_endpoint = f"https://{self.webapp_name}.azurewebsites.net/"
        self.foundry_endpoint = f"https://{self.foundry_account_name}.services.ai.azure.com"
        self.foundry_project = self.foundry_project_name
        self.assignee_object_id = os.getenv("ASSIGNEE_OBJECT_ID")
        self.is_staging = is_staging

    def __getattr__(self, name: str):
        try:
            return self.__dict__[name]
        except KeyError as exc:
            raise AttributeError(f"'{type(self).__name__}' object has no attribute '{name}'") from exc
