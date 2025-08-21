import os

from yaml import safe_load


class Variables:
    def __init__(self, *, is_staging: bool, path: str = "variables.yaml"):
        # verify that the file exists
        if not os.path.isfile(path):
            raise FileNotFoundError(f"Configuration file not found: {path}")
        with open(path, "r", encoding="utf-8") as stream:
            data = safe_load(stream)
            variables = [
                "RG_NAME",
                "RG_LOCATION",
                "SUBSCRIPTION_ID",
                "TENANT_ID",
                "ASSIGNEE_OBJECT_ID",
                "SEARCH_NAME",
                "SEARCH_INDEX_NAME",
                "VECTORIZER_PROFILE_NAME",
                "COSMOS_ACCOUNT_NAME",
                "COSMOS_DB_NAME",
                "COGNITIVE_SERVICES_NAME",
                "KEYVAULT_NAME",
                "APP_CONFIGURATION_NAME",
                "APP_SERVICE_PLAN_NAME",
                "WEBAPP_NAME",
                "FOUNDRY_ACCOUNT_NAME",
                "FOUNDRY_PROJECT_NAME",
                "FOUNDRY_KERNEL_MODEL",
                "FOUNDRY_API_VERSION",
                "OPENAI_NAME",
                "OPENAI_EMBEDDING_MODEL",
                "OPENAI_EMBEDDING_DIMENSIONS",
                "EVALS_RG",
                "EVALS_SUBSCRIPTION",
                "EVALS_PROJECT_NAME",
            ]
            missing = []
            for var in variables:
                val = data.get(var)
                if val is None:
                    missing.append(var)
                elif is_staging and var not in [
                    "RG_LOCATION",
                    "SUBSCRIPTION_ID",
                    "TENANT_ID",
                    "ASSIGNEE_OBJECT_ID",
                    "VECTORIZER_PROFILE_NAME",
                    "COSMOS_DB_NAME",
                    "OPENAI_RG",
                    "OPENAI_SUBSCRIPTION_ID",
                    "OPENAI_EMBEDDING_MODEL",
                    "OPENAI_EMBEDDING_DIMENSIONS",
                    "FOUNDRY_KERNEL_MODEL",
                    "FOUNDRY_API_VERSION",
                    "EVALS_RG",
                    "EVALS_SUBSCRIPTION",
                    "EVALS_PROJECT_NAME",
                ]:
                    val = f"{val}-staging"
                setattr(self, var.lower(), val)
            if missing:
                raise ValueError(f"Missing required variables: {', '.join(missing)}")
        self.search_endpoint = f"https://{self.search_name}.search.windows.net/"
        self.cosmos_endpoint = f"https://{self.cosmos_account_name}.documents.azure.com:443/"
        self.cognitive_services_id = f"/subscriptions/{self.subscription_id}/resourceGroups/{self.rg_name}/providers/Microsoft.CognitiveServices/accounts/{self.cognitive_services_name}"
        self.openai_endpoint = f"https://{self.openai_name}.openai.azure.com/"
        self.openai_id = f"/subscriptions/{self.subscription_id}/resourceGroups/{self.rg_name}/providers/Microsoft.CognitiveServices/accounts/{self.openai_name}"
        self.keyvault_endpoint = f"https://{self.keyvault_name}.vault.azure.net/"
        self.app_configuration_endpoint = f"https://{self.app_configuration_name}.azconfig.io"
        self.webapp_endpoint = f"https://{self.webapp_name}.azurewebsites.net/"
        self.foundry_endpoint = (
            f"https://{self.foundry_account_name}.services.ai.azure.com/api/projects/{self.foundry_project_name}"
        )
        self._is_staging = is_staging

    def __getattr__(self, name: str):
        try:
            return self.__dict__[name]
        except KeyError:
            raise AttributeError(f"'{type(self).__name__}' object has no attribute '{name}'")
