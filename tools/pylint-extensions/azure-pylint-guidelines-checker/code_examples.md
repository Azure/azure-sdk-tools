# Code Examples for Azure SDK Python Guidelines

This document contains code examples showing how to fix violations of the Azure SDK Python Guidelines.

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
- [client-docstring-use-literal-include](#client-docstring-use-literal-include)
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
- [do-not-log-exceptions-if-not-debug](#do-not-log-exceptions-if-not-debug)
- [unapproved-client-method-name-prefix](#unapproved-client-method-name-prefix)
- [do-not-hardcode-dedent](#do-not-hardcode-dedent)
- [client-lro-methods-use-polling](#client-lro-methods-use-polling)
- [lro-methods-use-correct-naming](#lro-methods-use-correct-naming)
- [missing-user-agent-policy](#missing-user-agent-policy)
- [missing-logging-policy](#missing-logging-policy)
- [missing-retry-policy](#missing-retry-policy)
- [missing-distributed-tracing-policy](#missing-distributed-tracing-policy)
- [do-not-use-logging-exception](#do-not-use-logging-exception)
- [remove-deprecated-iscoroutinefunction](#remove-deprecated-iscoroutinefunction)

## client-method-should-not-use-static-method

❌ **Incorrect**:
```python
class ExampleClient:
    @staticmethod
    def get_data(name):
        return {"name": name}
```

✅ **Correct**:
```python
class ExampleClient:
    def get_data(self, name):
        return {"name": name}

# Alternatively, use a module-level function
def get_data(name):
    return {"name": name}
```

## missing-client-constructor-parameter-credential

❌ **Incorrect**:
```python
class ExampleClient:
    def __init__(self, endpoint):
        self.endpoint = endpoint
```

✅ **Correct**:
```python
class ExampleClient:
    def __init__(self, endpoint, credential, **kwargs):
        self.endpoint = endpoint
        self.credential = credential
```

## missing-client-constructor-parameter-kwargs

❌ **Incorrect**:
```python
class ExampleClient:
    def __init__(self, endpoint, credential):
        self.endpoint = endpoint
        self.credential = credential
```

✅ **Correct**:
```python
class ExampleClient:
    def __init__(self, endpoint, credential, **kwargs):
        self.endpoint = endpoint
        self.credential = credential
```

## client-method-has-more-than-5-positional-arguments

❌ **Incorrect**:
```python
class ExampleClient:
    def update_item(self, item_id, name, description, version, status, category, tags):
        # Implementation
```

✅ **Correct**:
```python
class ExampleClient:
    def update_item(self, item_id, name, description, *, version, status, category, tags):
        # Implementation
```

## client-method-missing-type-annotations

❌ **Incorrect**:
```python
class ExampleClient:
    def get_item(self, item_id):
        # Implementation
```

✅ **Correct**:
```python
class ExampleClient:
    def get_item(self, item_id: str) -> Dict[str, Any]:
        # Implementation
```

## client-incorrect-naming-convention

❌ **Incorrect**:
```python
class exampleClient:  # lowercase first letter
    DEFAULT_value = "something"  # not all caps for constant
    
    def GetItem(self, itemId):  # camelCase method
        # Implementation
```

✅ **Correct**:
```python
class ExampleClient:  # PascalCase for class
    DEFAULT_VALUE = "something"  # UPPERCASE for constants
    
    def get_item(self, item_id):  # snake_case for methods and variables
        # Implementation
```

## client-method-missing-kwargs

❌ **Incorrect**:
```python
class ExampleClient:
    @distributed_trace
    def get_item(self, item_id: str) -> Dict[str, Any]:
        # Implementation
```

✅ **Correct**:
```python
class ExampleClient:
    @distributed_trace
    def get_item(self, item_id: str, **kwargs) -> Dict[str, Any]:
        # Implementation
```

## config-missing-kwargs-in-policy

❌ **Incorrect**:
```python
def create_configuration():
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy()
    return config
```

✅ **Correct**:
```python
def create_configuration():
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy(**kwargs)
    return config
```

## async-client-bad-name

❌ **Incorrect**:
```python
class ExampleAsyncClient:
    # Implementation
```

✅ **Correct**:
```python
class ExampleClient:  # Same name for both sync and async clients
    # Implementation
```

## file-needs-copyright-header

❌ **Incorrect**:
```python
"""Example module docstring."""

def example_function():
    # Implementation
```

✅ **Correct**:
```python
# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------

"""Example module docstring."""

def example_function():
    # Implementation
```

## client-method-name-no-double-underscore

❌ **Incorrect**:
```python
class ExampleClient:
    def __get_item(self, item_id: str) -> Dict[str, Any]:
        # Implementation
```

✅ **Correct**:
```python
class ExampleClient:
    def _get_item(self, item_id: str) -> Dict[str, Any]:
        # Implementation
```

## client-docstring-use-literal-include
❌ **Incorrect**:
```python
def example_function():
    """Example function.
    
    .. code-block:: python
    
       def example():
           pass
    """
```
✅ **Correct**:
```python
def example_function():
    """Example function.
    
    .. literalinclude:: ../samples/sample.py
    """
```

## specify-parameter-names-in-call

❌ **Incorrect**:
```python
def process(self):
    result = self.client.update_item("item1", "new name", "new description", 2, "active", "general")
```

✅ **Correct**:
```python
def process(self):
    result = self.client.update_item(
        "item1", 
        "new name", 
        "new description", 
        version=2, 
        status="active", 
        category="general"
    )
```

## connection-string-should-not-be-constructor-param

❌ **Incorrect**:
```python
class ExampleClient:
    def __init__(self, connection_string, **kwargs):
        # Implementation
```

✅ **Correct**:
```python
class ExampleClient:
    def __init__(self, endpoint, credential, **kwargs):
        # Implementation
        
    @classmethod
    def from_connection_string(cls, connection_string, **kwargs):
        # Parse connection string and create client
        return cls(endpoint, credential, **kwargs)
```

## package-name-incorrect

❌ **Incorrect**:
```python
# setup.py
PACKAGE_NAME = "azure.storage.blobs"
```

✅ **Correct**:
```python
# setup.py
PACKAGE_NAME = "azure-storage-blobs"
```

## client-suffix-needed

❌ **Incorrect**:
```python
class BlobService:
    # Implementation
```

✅ **Correct**:
```python
class BlobClient:
    # Implementation
```

## docstring-admonition-needs-newline

❌ **Incorrect**:
```python
def example_function():
    """Example function.
    
    .. admonition:: Example
       This is an example.
    .. literalinclude:: ../samples/sample.py
    """
```

✅ **Correct**:
```python
def example_function():
    """Example function.
    
    .. admonition:: Example
       This is an example.

    .. literalinclude:: ../samples/sample.py
    """
```

## naming-mismatch

❌ **Incorrect**:
```python
# __init__.py
from ._generated.models import ExampleModel as Example
__all__ = ["Example"]
```

✅ **Correct**:
```python
# __init__.py
from ._generated.models import ExampleModel
__all__ = ["ExampleModel"]
```

## client-accepts-api-version-keyword

❌ **Incorrect**:
```python
class ExampleClient:
    def __init__(self, endpoint, credential, **kwargs):
        # Implementation
```

✅ **Correct**:
```python
class ExampleClient:
    def __init__(self, endpoint, credential, *, api_version="2021-01-01", **kwargs):
        """
        :keyword str api_version: The API version to use for this operation.
        """
        # Implementation
```

## enum-must-be-uppercase

❌ **Incorrect**:
```python
class Color(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    red = "red"
    blue = "blue"
    green = "green"
```

✅ **Correct**:
```python
class Color(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    RED = "red"
    BLUE = "blue"
    GREEN = "green"
```

## enum-must-inherit-case-insensitive-enum-meta

❌ **Incorrect**:
```python
class Color(str, Enum):
    RED = "red"
    BLUE = "blue"
    GREEN = "green"
```

✅ **Correct**:
```python
from azure.core import CaseInsensitiveEnumMeta

class Color(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    RED = "red"
    BLUE = "blue"
    GREEN = "green"
```

## networking-import-outside-azure-core-transport

❌ **Incorrect**:
```python
import requests

def fetch_data(url):
    return requests.get(url)
```

✅ **Correct**:
```python
from azure.core.pipeline.transport import HttpRequest

def fetch_data(pipeline, url):
    request = HttpRequest("GET", url)
    return pipeline.run(request)
```

## non-abstract-transport-import

❌ **Incorrect**:
```python
from azure.core.pipeline.transport import RequestsTransport

def create_client(endpoint, credential, **kwargs):
    transport = RequestsTransport(**kwargs)
    # Implementation
```

✅ **Correct**:
```python
from azure.core.pipeline.transport import HttpTransport

def create_client(endpoint, credential, **kwargs):
    # Let caller or azure.core decide the transport
    # Implementation
```

## no-raise-with-traceback

❌ **Incorrect**:
```python
from azure.core.exceptions import raise_with_traceback

try:
    # some operation
except Exception as error:
    raise_with_traceback(
        ValueError("An error occurred"), error
    )
```

✅ **Correct**:
```python
try:
    # some operation
except Exception as error:
    raise ValueError("An error occurred") from error
```

## name-too-long

❌ **Incorrect**:
```python
def fetch_all_items_from_database_with_additional_metadata_included(client):
    # Implementation
```

✅ **Correct**:
```python
def fetch_all_items_with_metadata(client):
    # Implementation
```

## delete-operation-wrong-return-type

❌ **Incorrect**:
```python
class ExampleClient:
    def delete_item(self, item_id, **kwargs) -> Dict[str, Any]:
        return delete_response
```

✅ **Correct**:
```python
class ExampleClient:
    def delete_item(self, item_id, **kwargs) -> None:
        return None  # No return value for delete operation
```

## client-method-missing-tracing-decorator

❌ **Incorrect**:
```python
class ExampleClient:
    def get_item(self, item_id: str, **kwargs) -> Dict[str, Any]:
        # Implementation
```

✅ **Correct**:
```python
from azure.core.tracing.decorator import distributed_trace

class ExampleClient:
    @distributed_trace
    def get_item(self, item_id: str, **kwargs) -> Dict[str, Any]:
        # Implementation
```

## client-method-missing-tracing-decorator-async

❌ **Incorrect**:
```python
class ExampleAsyncClient:
    async def get_item(self, item_id: str, **kwargs) -> Dict[str, Any]:
        # Implementation
```

✅ **Correct**:
```python
from azure.core.tracing.decorator_async import distributed_trace_async

class ExampleAsyncClient:
    @distributed_trace_async
    async def get_item(self, item_id: str, **kwargs) -> Dict[str, Any]:
        # Implementation
```

## client-list-methods-use-paging

❌ **Incorrect**:
```python
def list_blobs(self, **kwargs) -> list:
    response = self._client.send_request("GET", "/blobs")
    return response.json()["value"]
```

✅ **Correct**:
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

❌ **Incorrect**:
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :return: The blob data.
    :rtype: bytes
    """
    # Missing :param name: documentation
```

✅ **Correct**:
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param str name: The name of the blob.
    :return: The blob data.
    :rtype: bytes
    """
```

## docstring-missing-type

❌ **Incorrect**:
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :return: The blob data.
    :rtype: bytes
    """
    # Missing :type name: documentation
```

✅ **Correct**:
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

❌ **Incorrect**:
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :type name: str
    :rtype: bytes
    """
    # Missing :return: documentation
```

✅ **Correct**:
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

❌ **Incorrect**:
```python
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param name: The name of the blob.
    :type name: str
    :return: The blob data.
    """
    # Missing :rtype: documentation
```

✅ **Correct**:
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

❌ **Incorrect**:
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

✅ **Correct**:
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

❌ **Incorrect**:
```python
import six  # Legacy compatibility library

def is_string(s):
    return isinstance(s, six.string_types)
```

✅ **Correct**:
```python
def is_string(s):
    return isinstance(s, str)  # Use native Python 3 types
```

## no-legacy-azure-core-http-response-import

❌ **Incorrect**:
```python
# Importing from legacy location
from azure.core.pipeline.transport import HttpResponse

def process_response(response: HttpResponse):
    return response.text()
```

✅ **Correct**:
```python
# Importing from new location
from azure.core.rest import HttpResponse

def process_response(response: HttpResponse):
    return response.text()
```

## docstring-keyword-should-match-keyword-only

❌ **Incorrect**:
```python
def create_blob(self, name, data, *, content_type=None):
    """Create a blob.
    
    :param name: The name of the blob.
    :type name: str
    :param data: The blob data.
    :type data: bytes
    :keyword content_settings: The content settings.  # Doesn't match parameter name
    :paramtype content_settings: dict
    ```
```

✅ **Correct**:
```python
def create_blob(self, name, data, *, content_type=None):
    """Create a blob.
    
    :param name: The name of the blob.
    :type name: str
    :param data: The blob data.
    :type data: bytes
    :keyword content_type: The content type of the blob.  # Matches parameter name
    :paramtype content_type: str
    ```
```

## docstring-type-do-not-use-class

❌ **Incorrect**:
```python
def get_client(self):
    """Get the client.
    
    :return: The client.
    :rtype: :class:`~azure.storage.blob.BlobClient`  # Using :class: syntax
    ```
```

✅ **Correct**:
```python
def get_client(self):
    """Get the client.
    
    :return: The client.
    :rtype: ~azure.storage.blob.BlobClient  # Direct reference without :class:
    ```
```

## no-typing-import-in-type-check

❌ **Incorrect**:
```python
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from typing import Dict, List  # Should not import from typing under TYPE_CHECKING
```

✅ **Correct**:
```python
from typing import Dict, List, TYPE_CHECKING

if TYPE_CHECKING:
    from .models import SomeModel  # Only import non-typing modules under TYPE_CHECKING
```

## do-not-log-raised-errors

❌ **Incorrect**:
```python
import logging

logger = logging.getLogger(__name__)

try:
    # some operation
except Exception as e:
    logger.error(f"Operation failed: {e}")  # Logging at error level before raising
    raise BlobError("Operation failed") from e
```

✅ **Correct**:
```python
import logging

logger = logging.getLogger(__name__)

try:
    # some operation
except Exception as e:
    logger.debug(f"Operation failed: {e.__name__}")
    # Don't log the exception details as-is before raising
    raise BlobError("Operation failed") from e
```

## do-not-use-legacy-typing

❌ **Incorrect**:
```python
def get_blob(self, name):
    # type: (str) -> bytes
    """Get a blob."""
    return self._client.get_blob(name)
```

✅ **Correct**:
```python
def get_blob(self, name: str) -> bytes:
    """Get a blob."""
    return self._client.get_blob(name)
```

## do-not-import-asyncio

❌ **Incorrect**:
```python
import asyncio  # Direct import of asyncio

async def get_blob(self):
    response = await self._client.get_blob()
    await asyncio.sleep(0.1)  # Using asyncio.sleep directly
    return response
```

✅ **Correct**:
```python
from azure.core.pipeline.transport import AsyncHttpTransport

async def get_blob(self):
    response = await self._client.get_blob()
    transport: AsyncHttpTransport = self._pipeline.context.transport  # Get the transport
    await transport.sleep(0.1)  # Use transport's sleep method
    return response
```

## invalid-use-of-overload

❌ **Incorrect**:
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

✅ **Correct**:
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

❌ **Incorrect**:
```python
from azure.core.pipeline.transport import RequestsTransport

def create_client():
    transport = RequestsTransport(connection_verify=False)  # Hardcoded to False
    # ...
```

✅ **Correct**:
```python
from azure.core.pipeline.transport import RequestsTransport

def create_client(verify_ssl=True):
    transport = RequestsTransport(connection_verify=verify_ssl)  # Configurable
    # ...
```

## do-not-log-exceptions-if-not-debug

❌ **Incorrect**:
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

✅ **Correct**:
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

❌ **Incorrect**:
```python
class BlobClient:
    def fetch_blob(self, name):  # Using unapproved verb
        pass
    
    def modify_properties(self, properties):  # Using unapproved verb
        pass
```

✅ **Correct**:
```python
class BlobClient:
    def get_blob(self, name):  # Using approved verb
        pass
    
    def update_properties(self, properties):  # Using approved verb
        pass
```

## do-not-hardcode-dedent

❌ **Incorrect**:
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

✅ **Correct**:
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

## client-lro-methods-use-polling
❌ **Incorrect**:
```python
class ExampleClient:
    def begin_long_running_operation(self, **kwargs):
        # Implementation that does not use polling
        return []
```
✅ **Correct**:
```python
class ExampleClient:
    def begin_long_running_operation(self, **kwargs):
        # Implementation that uses polling
        return LROPoller()
```

# lro-methods-use-correct-naming
❌ **Incorrect**:
```python
class ExampleClient:
    def long_running_operation(self, **kwargs):
        # Implementation that does not use correct naming
        return LROPoller()
```
✅ **Correct**:
```python
class ExampleClient:
    def begin_long_running_operation(self, **kwargs):
        # Implementation that uses correct naming
        return LROPoller()
```

## missing-user-agent-policy

❌ **Incorrect**:
```python
def create_configuration(**kwargs):
    config = Configuration()
    config.retry_policy = RetryPolicy()
    config.logging_policy = NetworkTraceLoggingPolicy()
    return config
```

✅ **Correct**:
```python
def create_configuration(**kwargs):
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy(**kwargs)
    config.retry_policy = RetryPolicy(**kwargs)
    config.logging_policy = NetworkTraceLoggingPolicy(**kwargs)
    return config
```

## missing-logging-policy

❌ **Incorrect**:
```python
def create_configuration(**kwargs):
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy(**kwargs)
    config.retry_policy = RetryPolicy(**kwargs)
    return config
```

✅ **Correct**:
```python
def create_configuration(**kwargs):
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy(**kwargs)
    config.retry_policy = RetryPolicy(**kwargs)
    config.logging_policy = NetworkTraceLoggingPolicy(**kwargs)
    return config
```

## missing-retry-policy

❌ **Incorrect**:
```python
def create_configuration(**kwargs):
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy(**kwargs)
    config.logging_policy = NetworkTraceLoggingPolicy(**kwargs)
    return config
```

✅ **Correct**:
```python
def create_configuration(**kwargs):
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy(**kwargs)
    config.retry_policy = RetryPolicy(**kwargs)
    config.logging_policy = NetworkTraceLoggingPolicy(**kwargs)
    return config
```

## missing-distributed-tracing-policy

❌ **Incorrect**:
```python
def create_configuration(**kwargs):
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy(**kwargs)
    config.retry_policy = RetryPolicy(**kwargs)
    config.logging_policy = NetworkTraceLoggingPolicy(**kwargs)
    return config
```

✅ **Correct**:
```python
def create_configuration(**kwargs):
    config = Configuration()
    config.user_agent_policy = UserAgentPolicy(**kwargs)
    config.retry_policy = RetryPolicy(**kwargs)
    config.logging_policy = NetworkTraceLoggingPolicy(**kwargs)
    config.distributed_tracing_policy = DistributedTracingPolicy(**kwargs)
    return config
```

## do-not-use-logging-exception
❌ **Incorrect**:
```python
import logging

logging.exception("An error occurred")  # Using logging.exception directly
```
✅ **Correct**:
```python
import logging
logger = logging.getLogger(__name__)
try:
    # some operation
except Exception as e:
    logger.debug("An error occurred", exc_info=e)  # Using logger.debug with exc_info
```

## remove-deprecated-iscoroutinefunction
❌ **Incorrect**:
```python
import asyncio

async def my_async_function():
    pass

# Using deprecated asyncio.iscoroutinefunction
if asyncio.iscoroutinefunction(my_async_function):
    print("This is a coroutine function")
```

```python
# Also incorrect - importing directly from asyncio
from asyncio import iscoroutinefunction

async def my_async_function():
    pass

if iscoroutinefunction(my_async_function):
    print("This is a coroutine function")
```

✅ **Correct**:
```python
import inspect

async def my_async_function():
    pass

# Using inspect.iscoroutinefunction instead
if inspect.iscoroutinefunction(my_async_function):
    print("This is a coroutine function")
```