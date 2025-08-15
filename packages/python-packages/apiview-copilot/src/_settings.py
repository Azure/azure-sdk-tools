import threading

from azure.appconfiguration import AzureAppConfigurationClient
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient


class SettingsManager:
    _instance = None
    _lock = threading.Lock()

    def __new__(cls, *args, **kwargs):
        if not cls._instance:
            with cls._lock:
                if not cls._instance:
                    cls._instance = super(SettingsManager, cls).__new__(cls)
        return cls._instance

    def __init__(self, app_config_endpoint=None):
        if hasattr(self, "_initialized") and self._initialized:
            return
        self._initialized = True
        self.credential = DefaultAzureCredential()
        self.app_config_endpoint = app_config_endpoint or "<YOUR_APP_CONFIG_ENDPOINT>"
        self.app_config_client = AzureAppConfigurationClient(self.app_config_endpoint, self.credential)
        self._keyvault_clients = {}

    def get(self, key):
        setting = self.app_config_client.get_configuration_setting(key=key)
        value = setting.value
        # If value is a Key Vault reference, fetch from Key Vault
        if value.startswith("https://") and ".vault.azure.net" in value:
            return self._get_secret_from_keyvault(value)
        return value

    def _get_secret_from_keyvault(self, secret_uri):
        # Parse the Key Vault URI
        # Example: https://<vault-name>.vault.azure.net/secrets/<secret-name>/<version>
        from urllib.parse import urlparse

        parsed = urlparse(secret_uri)
        vault_url = f"{parsed.scheme}://{parsed.hostname}"
        secret_path = parsed.path.strip("/").split("/")
        secret_name = secret_path[1] if len(secret_path) > 1 else secret_path[0]
        secret_version = secret_path[2] if len(secret_path) > 2 else None
        if vault_url not in self._keyvault_clients:
            self._keyvault_clients[vault_url] = SecretClient(vault_url=vault_url, credential=self.credential)
        client = self._keyvault_clients[vault_url]
        if secret_version:
            secret = client.get_secret(secret_name, secret_version)
        else:
            secret = client.get_secret(secret_name)
        return secret.value


# Usage:
# settings = SettingsManager(app_config_endpoint="https://<your-app-config>.azconfig.io")
# my_setting = settings.get("MySettingKey")
