from typing import Optional, Mapping, Any

from azure.core.configuration import Configuration
from azure.core.credentials import TokenCredential
from azure.core.pipeline.policies import RedirectPolicy, ProxyPolicy


# test_ignores_config_policies_with_kwargs
class TestIgnoresConfigPoliciesWithKwargs():  # @
    def create_configuration(self, **kwargs):  # @
        config = Configuration(**kwargs)
        config.headers_policy = StorageHeadersPolicy(**kwargs)
        config.user_agent_policy = StorageUserAgentPolicy(**kwargs)
        config.retry_policy = kwargs.get('retry_policy') or ExponentialRetry(**kwargs)
        config.redirect_policy = RedirectPolicy(**kwargs)
        config.logging_policy = StorageLoggingPolicy(**kwargs)
        config.proxy_policy = ProxyPolicy(**kwargs)
        return config

    @staticmethod
    def create_config(credential, api_version=None, **kwargs):  # @
        # type: (TokenCredential, Optional[str], Mapping[str, Any]) -> Configuration
        if api_version is None:
            api_version = KeyVaultClient.DEFAULT_API_VERSION
        config = KeyVaultClient.get_configuration_class(api_version, aio=False)(credential, **kwargs)
        config.authentication_policy = ChallengeAuthPolicy(credential, **kwargs)
        return config


# test_finds_config_policies_without_kwargs
class TestFindsConfigPoliciesWithoutKwargs():  # @
    def create_configuration(self, **kwargs):  # @
        config = Configuration(**kwargs)
        config.headers_policy = StorageHeadersPolicy(**kwargs)
        config.user_agent_policy = StorageUserAgentPolicy()  # @
        config.retry_policy = kwargs.get('retry_policy') or ExponentialRetry(**kwargs)
        config.redirect_policy = RedirectPolicy(**kwargs)
        config.logging_policy = StorageLoggingPolicy()  # @
        config.proxy_policy = ProxyPolicy()  # @
        return config

    @staticmethod
    def create_config(credential, api_version=None, **kwargs):  # @
        # type: (TokenCredential, Optional[str], Mapping[str, Any]) -> Configuration
        if api_version is None:
            api_version = KeyVaultClient.DEFAULT_API_VERSION
        config = KeyVaultClient.get_configuration_class(api_version, aio=False)(credential, **kwargs)
        config.authentication_policy = ChallengeAuthPolicy(credential)  # @
        return config


# test_ignores_policies_outside_create_config
class TestIgnoresPoliciesOutsideCreateConfig():
    def _configuration(self, **kwargs):  # @
        config = Configuration(**kwargs)
        config.headers_policy = StorageHeadersPolicy(**kwargs)
        config.user_agent_policy = StorageUserAgentPolicy(**kwargs)
        config.retry_policy = kwargs.get('retry_policy') or ExponentialRetry()
        config.redirect_policy = RedirectPolicy()
        config.logging_policy = StorageLoggingPolicy()
        config.proxy_policy = ProxyPolicy()
        return config

    @staticmethod
    def some_other_method(credential, api_version=None, **kwargs):  # @
        # type: (TokenCredential, Optional[str], Mapping[str, Any]) -> Configuration
        if api_version is None:
            api_version = KeyVaultClient.DEFAULT_API_VERSION
        config = KeyVaultClient.get_configuration_class(api_version, aio=False)(credential)
        config.authentication_policy = ChallengeAuthPolicy(credential)
        return config

