# Test file for beta SDK with ApiVersion enum containing preview API (allowed)

VERSION = "1.0.0b1"  # Beta version

from enum import Enum
from azure.core import CaseInsensitiveEnumMeta

class ApiVersion(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    """API versions supported by the service"""
    V2023_01_01 = "2023-01-01"
    V2023_05_01_PREVIEW = "2023-05-01-preview"  # Preview API in beta SDK - OK
    V2024_01_01 = "2024-01-01"

class ServiceClient:
    def __init__(self, api_version=ApiVersion.V2023_01_01):
        self._api_version = api_version
