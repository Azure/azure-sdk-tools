import json
import os
import threading

from azure.appconfiguration import AzureAppConfigurationClient
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import KeyVaultSecretIdentifier, SecretClient
from dotenv import load_dotenv

load_dotenv(override=True)


class SettingsManager:
    _instance = None
    _lock = threading.Lock()

    def __new__(cls, *args, **kwargs):
        if not cls._instance:
            with cls._lock:
                if not cls._instance:
                    cls._instance = super(SettingsManager, cls).__new__(cls)
        return cls._instance

    def __init__(self):
        # pylint: disable=access-member-before-definition
        if hasattr(self, "_initialized") and self._initialized:
            return
        self._initialized = True
        self.credential = DefaultAzureCredential()
        self.app_config_endpoint = os.getenv("AZURE_APP_CONFIG_ENDPOINT")
        if not self.app_config_endpoint:
            raise ValueError("AZURE_APP_CONFIG_ENDPOINT must be set in the environment.")
        self.label = os.getenv("ENVIRONMENT_NAME")
        if not self.label:
            raise ValueError("ENVIRONMENT_NAME must be set in the environment.")
        self.label = self.label.strip().lower()

        print(f"Using App Configuration endpoint: {self.app_config_endpoint} with OpenAI endpoint: {os.getenv('OPENAI_ENDPOINT')}")
        self.credential.get_token = self.get_token_wrapper
        self.credential.get_token_info = self.get_token_wrapper

        self.app_config_client = AzureAppConfigurationClient(self.app_config_endpoint, self.credential)
        self._keyvault_clients = {}
        self._cache = {}

    def get_token_wrapper(self, *scopes, **kwargs):
        print(f"Acquiring token for scopes: {scopes}")
        try:
            return self.credential.get_token(*scopes, **kwargs)
        except:
            return self.credential.get_token_info(*scopes, **kwargs)

    def get(self, key):
        key = key.strip().lower()
        cache_key = (key, self.label)
        if cache_key in self._cache:
            return self._cache[cache_key]
        print(f"Fetching key '{key}' with label '{self.label}' from App Configuration.")
        setting = self.app_config_client.get_configuration_setting(key=key, label=self.label)
        value = setting.value
        content_type = getattr(setting, "content_type", None)
        # Check for Key Vault reference by content type
        if content_type and content_type.startswith("application/vnd.microsoft.appconfig.keyvaultref+json"):
            try:
                parsed = json.loads(value)
                if isinstance(parsed, dict) and "uri" in parsed and ".vault.azure.net" in parsed["uri"]:
                    secret_value = self._get_secret_from_keyvault(parsed["uri"])
                    self._cache[cache_key] = secret_value
                    return secret_value
            except Exception:
                pass
        # Fallback: direct URI string
        if isinstance(value, str) and value.startswith("https://") and ".vault.azure.net" in value:
            secret_value = self._get_secret_from_keyvault(value)
            self._cache[cache_key] = secret_value
            return secret_value
        self._cache[cache_key] = value
        return value

    def purge(self, key):
        """Purge a key from the cache, or '*' to clear all."""
        if key == "*":
            self._cache.clear()
        else:
            cache_key = (key, self.label)
            self._cache.pop(cache_key, None)

    def _get_secret_from_keyvault(self, secret_uri):
        # Parse the Key Vault URI
        # Example: https://<vault-name>.vault.azure.net/secrets/<secret-name>/<version>
        parsed = KeyVaultSecretIdentifier(secret_uri)

        vault_url = parsed.vault_url
        secret_name = parsed.name
        secret_version = parsed.version
        if vault_url not in self._keyvault_clients:
            self._keyvault_clients[vault_url] = SecretClient(vault_url=vault_url, credential=self.credential)
        client = self._keyvault_clients[vault_url]
        if secret_version:
            secret = client.get_secret(secret_name, secret_version)
        else:
            secret = client.get_secret(secret_name)
        return secret.value
