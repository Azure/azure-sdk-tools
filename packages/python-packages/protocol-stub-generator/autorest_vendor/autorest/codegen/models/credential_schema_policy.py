# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from abc import abstractmethod
from typing import List
from .credential_schema import CredentialSchema

class CredentialSchemaPolicy:

    def __init__(self, credential: CredentialSchema, *args, **kwargs) -> None:  # pylint: disable=unused-argument
        self.credential = credential

    @abstractmethod
    def call(self, async_mode: bool) -> str:
        ...

    @classmethod
    def name(cls):
        return cls.__name__


class BearerTokenCredentialPolicy(CredentialSchemaPolicy):

    def __init__(
        self,
        credential: CredentialSchema,
        credential_scopes: List[str]
    ) -> None:
        super().__init__(credential)
        self._credential_scopes = credential_scopes

    @property
    def credential_scopes(self):
        return self._credential_scopes

    def call(self, async_mode: bool) -> str:
        policy_name = f"Async{self.name()}" if async_mode else self.name()
        return f"policies.{policy_name}(self.credential, *self.credential_scopes, **kwargs)"


class AzureKeyCredentialPolicy(CredentialSchemaPolicy):

    def __init__(
        self,
        credential: CredentialSchema,
        credential_key_header_name: str
    ) -> None:
        super().__init__(credential)
        self._credential_key_header_name = credential_key_header_name

    @property
    def credential_key_header_name(self):
        return self._credential_key_header_name

    def call(self, async_mode: bool) -> str:
        return f'policies.AzureKeyCredentialPolicy(self.credential, "{self.credential_key_header_name}", **kwargs)'

def get_credential_schema_policy_type(name):
    policies = [BearerTokenCredentialPolicy, AzureKeyCredentialPolicy]
    try:
        return next(p for p in policies if p.name().lower() == name.lower())
    except StopIteration:
        raise ValueError(
            "The credential policy you pass in with --credential-default-policy-type must be either "
            "{}".format(
                " or ".join([p.name() for p in policies])
            )
        )
