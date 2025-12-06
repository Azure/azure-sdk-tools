# Test file for stable SDK with ApiVersion enum containing preview API

VERSION = "1.0.0"  # Stable version

from enum import Enum
from azure.core import CaseInsensitiveEnumMeta

class ApiVersion(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    """API versions supported by the service"""
    V2023_01_01 = "2023-01-01"  # Stable API - OK
    V2023_05_01_PREVIEW = "2023-05-01-preview"  # @  Preview API in stable SDK - Error
    V2024_01_01 = "2024-01-01"  # Stable API - OK
    V2024_06_01_PREVIEW = "2024-06-01-preview"  # @  Preview API in stable SDK - Error

class ServiceClient:
    def __init__(self, api_version=ApiVersion.V2023_01_01):
        self._api_version = api_version
