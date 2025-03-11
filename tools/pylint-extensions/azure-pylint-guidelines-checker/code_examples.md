# Code Examples for Pylint Guidelines

This document contains code examples for each pylint rule in the Azure SDK guidelines.

## Table of Contents

- [client-method-should-not-use-static-method](#client-method-should-not-use-static-method)
- [missing-client-constructor-parameter-credential](#missing-client-constructor-parameter-credential)
- [missing-client-constructor-parameter-kwargs](#missing-client-constructor-parameter-kwargs)
- [client-method-has-more-than-5-positional-arguments](#client-method-has-more-than-5-positional-arguments)
- [client-method-missing-type-annotations](#client-method-missing-type-annotations)
- [client-incorrect-naming-convention](#client-incorrect-naming-convention)
- [client-method-missing-kwargs](#client-method-missing-kwargs)
- [config-missing-kwargs-in-policy](#config-missing-kwargs-in-policy)
- [async-client-bad-name](#async-client-bad-name)
- [file-needs-copyright-header](#file-needs-copyright-header)
- [client-method-name-no-double-underscore](#client-method-name-no-double-underscore)
- [specify-parameter-names-in-call](#specify-parameter-names-in-call)
- [connection-string-should-not-be-constructor-param](#connection-string-should-not-be-constructor-param)
- [package-name-incorrect](#package-name-incorrect)
- [client-suffix-needed](#client-suffix-needed)
- [docstring-admonition-needs-newline](#docstring-admonition-needs-newline)
- [naming-mismatch](#naming-mismatch)
- [client-accepts-api-version-keyword](#client-accepts-api-version-keyword)
- [enum-must-be-uppercase](#enum-must-be-uppercase)
- [enum-must-inherit-case-insensitive-enum-meta](#enum-must-inherit-case-insensitive-enum-meta)
- [networking-import-outside-azure-core-transport](#networking-import-outside-azure-core-transport)
- [non-abstract-transport-import](#non-abstract-transport-import)
- [no-raise-with-traceback](#no-raise-with-traceback)
- [name-too-long](#name-too-long)
- [delete-operation-wrong-return-type](#delete-operation-wrong-return-type)
- [client-method-missing-tracing-decorator](#client-method-missing-tracing-decorator)
- [client-method-missing-tracing-decorator-async](#client-method-missing-tracing-decorator-async)
- [client-list-methods-use-paging](#client-list-methods-use-paging)
- [docstring-missing-param](#docstring-missing-param)
- [docstring-missing-type](#docstring-missing-type)
- [docstring-missing-return](#docstring-missing-return)
- [docstring-missing-rtype](#docstring-missing-rtype)
- [docstring-should-be-keyword](#docstring-should-be-keyword)
- [do-not-import-legacy-six](#do-not-import-legacy-six)
- [no-legacy-azure-core-http-response-import](#no-legacy-azure-core-http-response-import)
- [docstring-keyword-should-match-keyword-only](#docstring-keyword-should-match-keyword-only)
- [docstring-type-do-not-use-class](#docstring-type-do-not-use-class)
- [no-typing-import-in-type-check](#no-typing-import-in-type-check)
- [do-not-log-raised-errors](#do-not-log-raised-errors)
- [do-not-use-legacy-typing](#do-not-use-legacy-typing)
- [do-not-import-asyncio](#do-not-import-asyncio)
- [invalid-use-of-overload](#invalid-use-of-overload)
- [do-not-hardcode-connection-verify](#do-not-hardcode-connection-verify)
- [do-not-log-exceptions](#do-not-log-exceptions)
- [unapproved-client-method-name-prefix](#unapproved-client-method-name-prefix)
- [do-not-hardcode-dedent](#do-not-hardcode-dedent)

## client-method-should-not-use-static-method

**Incorrect:**
```python
class BlobClient:
    @staticmethod
    def parse_url(url):
        # Parse the URL
        return parsed_url
```

**Correct:**
```python
# Move to module level function
def parse_blob_url(url):
    # Parse the URL
    return parsed_url

class BlobClient:
    # No static methods in the client class
    pass
```

## missing-client-constructor-parameter-credential

**Incorrect:**
```python
class BlobClient:
    def __init__(self, endpoint):
        self._endpoint = endpoint
        # Missing credential parameter
```

**Correct:**
```python
class BlobClient:
    def __init__(self, endpoint, credential):
        self._endpoint = endpoint
        self._credential = credential
```

## missing-client-constructor-parameter-kwargs

**Incorrect:**
```python
class BlobClient:
    def __init__(self, endpoint, credential):
        self._endpoint = endpoint
        self._credential = credential
        # Missing **kwargs
```

**Correct:**
```python
class BlobClient:
    def __init__(self, endpoint, credential, **kwargs):
        self._endpoint = endpoint
        self._credential = credential
        # **kwargs allows for future additions without breaking changes
```

## client-method-has-more-than-5-positional-arguments

**Incorrect:**
```python
def create_blob(self, name, data, content_type, metadata, cache_control, content_disposition):
    # Too many positional arguments
    pass
```

**Correct:**
```python
def create_blob(self, name, data, content_type, metadata, cache_control, *, content_disposition=None):
    # Use keyword-only arguments (after *) for the 6th and beyond parameters
    pass
```

## client-method-missing-type-annotations

**Incorrect:**
```python
def get_blob(self, name):
    # Missing type annotations
    return self._client.get_blob(name)
```

**Correct:**
```python
def get_blob(self, name: str) -> bytes:
    # Type annotations provided
    return self._client.get_blob(name)
```

## client-incorrect-naming-convention

**Incorrect:**
```python
class blobClient:  # Should use PascalCase for class names
    def GetBlob(self):  # Should use snake_case for methods
        my_Constant = 42  # Should use ALL_CAPS for constants
        return my_Constant
```

**Correct:**
```python
class BlobClient:  # PascalCase for class names
    def get_blob(self):  # snake_case for methods
        MY_CONSTANT = 42  # ALL_CAPS for constants
        return MY_CONSTANT
```

## client-method-missing-kwargs

**Incorrect:**
```python
def get_blob(self, name):
    # Method making network calls should have **kwargs
    return self._client.send_request("GET", f"/blobs/{name}")
```

**Correct:**
```python
def get_blob(self, name, **kwargs):
    # **kwargs allows passing additional options to the network call
    return self._client.send_request("GET", f"/blobs/{name}", **kwargs)
```

## config-missing-kwargs-in-policy

**Incorrect:**
```python
def create_configuration(endpoint, **kwargs):
    return {
        "policies": [
            RetryPolicy(),  # Missing **kwargs
            BearerTokenCredentialPolicy(),  # Missing **kwargs
        ]
    }
```

**Correct:**
```python
def create_configuration(endpoint, **kwargs):
    return {
        "policies": [
            RetryPolicy(**kwargs),
            BearerTokenCredentialPolicy(**kwargs),
        ]
    }
```

## async-client-bad-name

**Incorrect:**
```python
class BlobAsyncClient:  # "Async" should not be in the class name
    async def get_blob(self, name):
        pass
```

**Correct:**
```python
class BlobClient:  # Same name as sync client
    async def get_blob(self, name):
        pass
```

## file-needs-copyright-header

**Incorrect:**
```python
# File without copyright header
from azure.core import *

# Code starts here
```

**Correct:**
```python
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from azure.core import *

# Code starts here
```

## client-method-name-no-double-underscore

**Incorrect:**
```python
class BlobClient:
    def __internal_method(self):  # Double underscore prefix is not allowed
        pass
```

**Correct:**
```python
class BlobClient:
    def _internal_method(self):  # Single underscore for non-public methods
        pass
```

## specify-parameter-names-in-call

**Incorrect:**
```python
def process_blob(self, name):
    # Calling with multiple unnamed parameters
    self._client.upload_blob(name, data, "text/plain", {"key": "value"}, 3600)
```

**Correct:**
```python
def process_blob(self, name):
    # Named parameters after the second argument
    self._client.upload_blob(
        name,
        data,
        content_type="text/plain",
        metadata={"key": "value"},
        timeout=3600
    )
```

## connection-string-should-not-be-constructor-param

**Incorrect:**
```python
class BlobClient:
    def __init__(self, connection_string, **kwargs):
        # Connection string in constructor
        self._parse_connection_string(connection_string)
```

**Correct:**
```python
class BlobClient:
    def __init__(self, endpoint, credential, **kwargs):
        self._endpoint = endpoint
        self._credential = credential
    
    @classmethod
    def from_connection_string(cls, connection_string, **kwargs):
        # Factory method for connection string
        endpoint, credential = cls._parse_connection_string(connection_string)
        return cls(endpoint, credential, **kwargs)
```

## package-name-incorrect

**Incorrect:**
```python
# setup.py with incorrect package name
setup(
    name="azure_storage_blob",  # Using underscores instead of dashes
    version="1.0.0",
    # ...
)
```

**Correct:**
```python
# setup.py with correct package name
setup(
    name="azure-storage-blob",  # Using dashes
    version="1.0.0",
    # ...
)
```

## client-suffix-needed

**Incorrect:**
```python
class Blob:  # Missing "Client" suffix
    def get_properties(self):
        pass
```

**Correct:**
```python
class BlobClient:  # Has "Client" suffix
    def get_properties(self):
        pass
```

## docstring-admonition-needs-newline

**Incorrect:**
```python
def get_examples():
    """Get examples for the API.
    .. literalinclude:: ../samples/example.py
        :start-after: [START get_examples]
        :end-before: [END get_examples]
        :language: python
    """
    pass
```

**Correct:**
```python
def get_examples():
    """Get examples for the API.

    .. literalinclude:: ../samples/example.py
        :start-after: [START get_examples]
        :end-before: [END get_examples]
        :language: python
    """
    pass
```

## naming-mismatch

**Incorrect:**
```python
# In generated_models.py
class BlobProperties:
    pass

# In client code
from .generated_models import BlobProperties as BlobProps  # aliased name
```

**Correct:**
```python
# In generated_models.py
class BlobProperties:
    pass

# In client code
from .generated_models import BlobProperties  # use original name
```

## client-accepts-api-version-keyword

**Incorrect:**
```python
class BlobClient:
    def __init__(self, endpoint, credential, **kwargs):
        # Missing api_version parameter
        self._endpoint = endpoint
```

**Correct:**
```python
class BlobClient:
    def __init__(self, endpoint, credential, *, api_version="2020-06-12", **kwargs):
        # Keyword-only api_version parameter
        self._endpoint = endpoint
        self._api_version = api_version
```

## enum-must-be-uppercase

**Incorrect:**
```python
from enum import Enum

class AccessType(str, Enum):  # Enum name should be uppercase
    Private = "private"
    Public = "public"
```

**Correct:**
```python
from enum import Enum

class ACCESS_TYPE(str, Enum):  # Uppercase enum name
    PRIVATE = "private"
    PUBLIC = "public"
```

## enum-must-inherit-case-insensitive-enum-meta

**Incorrect:**
```python
from enum import Enum

class ACCESS_TYPE(str, Enum):  # Missing CaseInsensitiveEnumMeta
    PRIVATE = "private"
    PUBLIC = "public"
```

**Correct:**
```python
from enum import Enum
from azure.core.enum_base import CaseInsensitiveEnumMeta

class ACCESS_TYPE(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    PRIVATE = "private"
    PUBLIC = "public"
```

## networking-import-outside-azure-core-transport

**Incorrect:**
```python
# In a client library outside azure.core
import requests  # Direct networking import

def get_data(url):
    return requests.get(url)
```

**Correct:**
```python
# In a client library outside azure.core
from azure.core.rest import HttpRequest
from azure.core.pipeline import Pipeline

def get_data(url, pipeline):
    request = HttpRequest("GET", url)
    return pipeline.run(request)
```

## non-abstract-transport-import

**Incorrect:**
```python
# Importing a specific transport implementation
from azure.core.pipeline.transport import RequestsTransport

def create_client(endpoint):
    transport = RequestsTransport()
    # ...
```

**Correct:**
```python
# Import only the abstract transport
from azure.core.pipeline.transport import HttpTransport

def create_client(endpoint, transport=None):
    transport = transport or HttpTransport()
    # ...
```

## no-raise-with-traceback

**Incorrect:**
```python
from azure.core.exceptions import raise_with_traceback

try:
    # some operation
except Exception as e:
    raise_with_traceback(BlobError, e)
```

**Correct:**
```python
try:
    # some operation
except Exception as e:
    raise BlobError("Failed to perform operation") from e
```

## name-too-long

**Incorrect:**
```python
def download_blob_and_verify_content_with_checksum_validation(self, blob_name):
    # Method name is too long (over 40 characters)
    pass
```

**Correct:**
```python
def download_blob_with_checksum(self, blob_name):
    # Shorter name (under 40 characters)
    pass
```

## delete-operation-wrong-return-type

**Incorrect:**
```python
def delete_blob(self, name, **kwargs) -> dict:
    response = self._client.send_request("DELETE", f"/blobs/{name}")
    return response.json()
```

**Correct:**
```python
def delete_blob(self, name, **kwargs) -> None:
    self._client.send_request("DELETE", f"/blobs/{name}")
    # Return None for delete operations
```

## client-method-missing-tracing-decorator

**Incorrect:**
```python
# Missing distributed_trace decorator
def get_blob(self, name, **kwargs):
    return self._client.send_request("GET", f"/blobs/{name}")
```

**Correct:**
```python
from azure.core.tracing.decorator import distributed_trace

@distributed_trace
def get_blob(self, name, **kwargs):
    return self._client.send_request("GET", f"/blobs/{name}")
```

## client-method-missing-tracing-decorator-async

**Incorrect:**
```python
# Missing distributed_trace_async decorator
async def get_blob(self, name, **kwargs):
    return await self._client.send_request("GET", f"/blobs/{name}")
```

**Correct:**
```python
from azure.core.tracing.decorator_async import distributed_trace_async

@distributed_trace_async
async def get_blob(self, name, **kwargs):
    return await self._client.send_request("GET", f"/blobs/{name}")
```

## client-list-methods-use-paging

**Incorrect:**
```python
def list_blobs(self, **kwargs) -> list:
    response = self._client.send_request("GET", "/blobs")
    return response.json()["value"]
```

**Correct:**
```python
from azure.core.paging import ItemPaged

def list_blobs(self, **kwargs) -> ItemPaged:
    def get_next(continuation_token=None):
        params = {} if continuation_token is None else {"marker": continuation_token}
        response = self._client.send_request("GET", "/blobs", params=params)
        return response.json()
    
    return ItemPaged(get_next, extract_data=lambda r: r["value"])
```

## docstring-missing-param

**Incorrect:**
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :return: The blob data.
    :rtype: bytes
    """
    # Missing :param name: documentation
```

**Correct:**
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :return: The blob data.
    :rtype: bytes
    """
```

## docstring-missing-type

**Incorrect:**
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :return: The blob data.
    :rtype: bytes
    """
    # Missing :type name: documentation
```

**Correct:**
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :type name: str
    :return: The blob data.
    :rtype: bytes
    """
```

## docstring-missing-return

**Incorrect:**
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :type name: str
    :rtype: bytes
    """
    # Missing :return: documentation
```

**Correct:**
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :type name: str
    :return: The blob data.
    :rtype: bytes
    """
```

## docstring-missing-rtype

**Incorrect:**
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :type name: str
    :return: The blob data.
    """
    # Missing :rtype: documentation
```

**Correct:**
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :type name: str
    :return: The blob data.
    :rtype: bytes
    """
```

## docstring-should-be-keyword

**Incorrect:**
```python
def create_blob(self, name, data, *, content_type=None, **kwargs):
    """Create a blob.
    
    :param name: The name of the blob.
    :type name: str
    :param data: The blob data.
    :type data: bytes
    :param content_type: The content type of the blob.  # Should use :keyword: for keyword-only args
    :type content_type: str
    """
```

**Correct:**
```python
def create_blob(self, name, data, *, content_type=None, **kwargs):
    """Create a blob.
    
    :param name: The name of the blob.
    :type name: str
    :param data: The blob data.
    :type data: bytes
    :keyword content_type: The content type of the blob.
    :paramtype content_type: str
    """
```

## do-not-import-legacy-six

**Incorrect:**
```python
import six  # Legacy compatibility library

def is_string(s):
    return isinstance(s, six.string_types)
```

**Correct:**
```python
def is_string(s):
    return isinstance(s, str)  # Use native Python 3 types
```

## no-legacy-azure-core-http-response-import

**Incorrect:**
```python
# Importing from legacy location
from azure.core.pipeline.transport import HttpResponse

def process_response(response: HttpResponse):
    return response.text()
```

**Correct:**
```python
# Importing from new location
from azure.core.rest import HttpResponse

def process_response(response: HttpResponse):
    return response.text()
```

## docstring-keyword-should-match-keyword-only

**Incorrect:**
```python
def create_blob(self, name, data, *, content_type=None):
    """Create a blob.
    
    :param name: The name of the blob.
    :type name: str
    :param data: The blob data.
    :type data: bytes
    :keyword content_settings: The content settings.  # Doesn't match parameter name
    :paramtype content_settings: dict
    """
```

**Correct:**
```python
def create_blob(self, name, data, *, content_type=None):
    """Create a blob.
    
    :param name: The name of the blob.
    :type name: str
    :param data: The blob data.
    :type data: bytes
    :keyword content_type: The content type of the blob.  # Matches parameter name
    :paramtype content_type: str
    """
```

## docstring-type-do-not-use-class

**Incorrect:**
```python
def get_client(self):
    """Get the client.
    
    :return: The client.
    :rtype: :class:`~azure.storage.blob.BlobClient`  # Using :class: syntax
    """
```

**Correct:**
```python
def get_client(self):
    """Get the client.
    
    :return: The client.
    :rtype: ~azure.storage.blob.BlobClient  # Direct reference without :class:
    """
```

## no-typing-import-in-type-check

**Incorrect:**
```python
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from typing import Dict, List  # Should not import from typing under TYPE_CHECKING
```

**Correct:**
```python
from typing import Dict, List, TYPE_CHECKING

if TYPE_CHECKING:
    from .models import SomeModel  # Only import non-typing modules under TYPE_CHECKING
```

## do-not-log-raised-errors

**Incorrect:**
```python
import logging

logger = logging.getLogger(__name__)

try:
    # some operation
except Exception as e:
    logger.error(f"Operation failed: {e}")  # Logging at error level before raising
    raise BlobError("Operation failed") from e
```

**Correct:**
```python
import logging

logger = logging.getLogger(__name__)

try:
    # some operation
except Exception as e:
    logger.debug(f"Operation failed: {e}")  # Log at debug level before raising
    # Or just don't log the exception details at all
    raise BlobError("Operation failed") from e
```

## do-not-use-legacy-typing

**Incorrect:**
```python
def get_blob(self, name):
    # type: (str) -> bytes
    """Get a blob."""
    return self._client.get_blob(name)
```

**Correct:**
```python
def get_blob(self, name: str) -> bytes:
    """Get a blob."""
    return self._client.get_blob(name)
```

## do-not-import-asyncio

**Incorrect:**
```python
import asyncio  # Direct import of asyncio

async def get_blob(self):
    response = await self._client.get_blob()
    await asyncio.sleep(0.1)  # Using asyncio.sleep directly
    return response
```

**Correct:**
```python
from azure.core.pipeline.transport import AsyncHttpTransport

async def get_blob(self):
    response = await self._client.get_blob()
    transport: AsyncHttpTransport = self._pipeline.context.transport  # Get the transport
    await transport.sleep(0.1)  # Use transport's sleep method
    return response
```

## invalid-use-of-overload

**Incorrect:**
```python
from typing import overload

class BlobClient:
    @overload
    def get_blob(self, name: str) -> bytes:
        ...
    
    @overload
    async def get_blob(self, name: str) -> bytes:  # Mixing sync and async
        ...
    
    def get_blob(self, name):
        # implementation
        pass
```

**Correct:**
```python
from typing import overload

class BlobClient:
    @overload
    def get_blob(self, name: str) -> bytes:
        ...
    
    @overload
    def get_blob(self, name: str, max_size: int) -> bytes:  # Same sync/async context
        ...
    
    def get_blob(self, name, max_size=None):
        # implementation
        pass
```

## do-not-hardcode-connection-verify

**Incorrect:**
```python
from azure.core.pipeline.transport import RequestsTransport

def create_client():
    transport = RequestsTransport(connection_verify=False)  # Hardcoded to False
    # ...
```

**Correct:**
```python
from azure.core.pipeline.transport import RequestsTransport

def create_client(verify_ssl=True):
    transport = RequestsTransport(connection_verify=verify_ssl)  # Configurable
    # ...
```

## do-not-log-exceptions

**Incorrect:**
```python
import logging

logger = logging.getLogger(__name__)

def get_blob(name):
    try:
        response = self._client.get_blob(name)
        return response
    except Exception as e:
        logger.error(f"Error details: {e}")  # Logging exception at error level
        raise
```

**Correct:**
```python
import logging

logger = logging.getLogger(__name__)

def get_blob(name):
    try:
        response = self._client.get_blob(name)
        return response
    except Exception as e:
        logger.debug(f"Error details: {str(e)}")  # Log details at debug level only
        logger.error("Failed to get blob")  # Higher level logs without sensitive details
        raise
```

## unapproved-client-method-name-prefix

**Incorrect:**
```python
class BlobClient:
    def fetch_blob(self, name):  # Using unapproved verb
        pass
    
    def modify_properties(self, properties):  # Using unapproved verb
        pass
```

**Correct:**
```python
class BlobClient:
    def get_blob(self, name):  # Using approved verb
        pass
    
    def update_properties(self, properties):  # Using approved verb
        pass
```

## do-not-hardcode-dedent

**Incorrect:**
```python
def get_examples():
    """Get examples for the API.

    .. literalinclude:: ../samples/example.py
        :start-after: [START get_examples]
        :end-before: [END get_examples]
        :language: python
        :dedent: 4
    """
    pass

```

**Correct:**
```python
def get_examples():
    """Get examples for the API.

    .. literalinclude:: ../samples/example.py
        :start-after: [START get_examples]
        :end-before: [END get_examples]
        :language: python
        :dedent:
    """
    pass

```