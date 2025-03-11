
## Code Examples

This section provides examples of incorrect and correct code for custom linting rules.

### Client Constructor Examples

#### missing-client-constructor-parameter-credential

```python
# Incorrect - Missing credential parameter
class BlobClient:
    def __init__(self, endpoint, **kwargs):
        self._endpoint = endpoint

# Correct
class BlobClient:
    def __init__(self, endpoint, credential, **kwargs):
        self._endpoint = endpoint
        self._credential = credential
```

#### missing-client-constructor-parameter-kwargs

```python
# Incorrect - Missing **kwargs parameter
class BlobClient:
    def __init__(self, endpoint, credential):
        self._endpoint = endpoint
        self._credential = credential

# Correct
class BlobClient:
    def __init__(self, endpoint, credential, **kwargs):
        self._endpoint = endpoint
        self._credential = credential
```

#### client-accepts-api-version-keyword

```python
# Incorrect - Missing api_version parameter
class BlobClient:
    def __init__(self, endpoint, credential, **kwargs):
        self._endpoint = endpoint
        self._credential = credential

# Correct
class BlobClient:
    def __init__(self, endpoint, credential, *, api_version="2020-04-08", **kwargs):
        self._endpoint = endpoint
        self._credential = credential
        self._api_version = api_version
```

### Method Examples

#### client-method-should-not-use-static-method

```python
# Incorrect - Using static method in client class
class BlobClient:
    @staticmethod
    def format_url(account, container):
        return f"https://{account}.blob.core.windows.net/{container}"

# Correct - Move to module-level function
def format_blob_url(account, container):
    return f"https://{account}.blob.core.windows.net/{container}"

class BlobClient:
    # No static methods
    pass
```

#### client-method-missing-kwargs

```python
# Incorrect - Method making network call without **kwargs
class BlobClient:
    def get_blob(self, name):
        return self._transport_pipeline.request(...)

# Correct
class BlobClient:
    def get_blob(self, name, **kwargs):
        return self._transport_pipeline.request(..., **kwargs)
```

#### client-method-has-more-than-5-positional-arguments

```python
# Incorrect - Too many positional arguments
def create_container(self, name, metadata, public_access, timeout, etag, match_condition, lease_id):
    pass

# Correct - Use keyword-only arguments after the 5th argument
def create_container(self, name, metadata, public_access, timeout, etag, *, 
                    match_condition=None, lease_id=None):
    pass
```

### Naming Examples

#### client-incorrect-naming-convention

```python
# Incorrect - Wrong naming conventions
class blobClient:  # Should be PascalCase
    def GetBlob(self):  # Should be snake_case
        my_Constant = "VALUE"  # Constants should be ALL_CAPS
        return my_Constant

# Correct
class BlobClient:
    def get_blob(self):
        MY_CONSTANT = "VALUE"
        return MY_CONSTANT
```

#### enum-must-be-uppercase

```python
# Incorrect - Enum name not uppercase
class Colors(str, Enum):
    Red = "red"
    Blue = "blue"

# Correct
class COLORS(str, Enum):
    RED = "red"
    BLUE = "blue"
```

### Documentation Examples

#### docstring-missing-param

```python
# Incorrect - Missing parameter documentation
def get_blob(self, name, **kwargs):
    """Get a blob from the container.

    :return: The blob data.
    :rtype: bytes
    """
    pass

# Correct
def get_blob(self, name, **kwargs):
    """Get a blob from the container.
    
    :param str name: The name of the blob.
    :return: The blob data.
    :rtype: bytes
    """
    pass
```

#### docstring-keyword-should-match-keyword-only

```python
# Incorrect - Keyword doc doesn't match keyword-only parameters
def create_blob(self, name, data, *, content_type=None):
    """Create a blob.
    
    :param str name: The name of the blob.
    :param str data: The blob data.
    :keyword content_settings: The content settings. (MISMATCH)
    """
    pass

# Correct
def create_blob(self, name, data, *, content_type=None):
    """Create a blob.
    
    :param str name: The name of the blob.
    :param str data: The blob data.
    :keyword cstr or None content_type: The content type of the blob.
    """
    pass
```

### Error Handling Examples

#### no-raise-with-traceback

```python
# Incorrect - Using legacy raise_with_traceback
from azure.core.exceptions import raise_with_traceback
try:
    # Some operation
except Exception as error:
    raise_with_traceback(BlobError, error)

# Correct - Using Python 3 raise from
try:
    # Some operation
except Exception as error:
    raise BlobError("Operation failed") from error
```

#### do-not-log-raised-errors

```python
# Incorrect - Logging errors at error level when raising
try:
    # Some operation
except Exception as error:
    logger.error("Operation failed: %s", error)
    raise BlobError("Operation failed") from error

# Correct - Use debug level for logging when raising
try:
    # Some operation
except Exception as error:
    logger.debug("Operation failed: %s", error)
    raise BlobError("Operation failed") from error
```

### Type Annotation Examples

#### client-method-missing-type-annotations

```python
# Incorrect - Missing type annotations
def get_blob(self, name):
    """Get a blob from the container."""
    return self._client.get_blob(name)

# Correct - Using type annotations
def get_blob(self, name: str) -> bytes:
    """Get a blob from the container."""
    return self._client.get_blob(name)
```

#### do-not-use-legacy-typing

```python
# Incorrect - Using legacy type comments
def get_blob(self, name):  # type: (str) -> bytes
    """Get a blob from the container."""
    return self._client.get_blob(name)

# Correct - Using modern type annotations (Python 3.8+)
def get_blob(self, name: str) -> bytes:
    """Get a blob from the container."""
    return self._client.get_blob(name)
```

### Client Design Examples

#### async-client-bad-name

```python
# Incorrect - Has "Async" in client name
class BlobAsyncClient:
    async def get_blob(self, name: str) -> bytes:
        pass

# Correct - No "Async" in client name
class BlobClient:  # Same name for both sync and async clients
    async def get_blob(self, name: str) -> bytes:
        pass
```

#### client-suffix-needed

```python
# Incorrect - Missing "Client" suffix
class Blob:
    def get_blob(self, name: str) -> bytes:
        pass

# Correct - Has "Client" suffix
class BlobClient:
    def get_blob(self, name: str) -> bytes:
        pass
```

#### connection-string-should-not-be-constructor-param

```python
# Incorrect - Connection string in constructor
class BlobClient:
    def __init__(self, connection_string: str, **kwargs):
        self._conn_str = connection_string

# Correct - Use a factory method instead
class BlobClient:
    def __init__(self, endpoint: str, credential: Any, **kwargs):
        self._endpoint = endpoint
        self._credential = credential
    
    @classmethod
    def from_connection_string(cls, connection_string: str, **kwargs) -> "BlobClient":
        # Parse connection string and create client
        endpoint = parse_endpoint(connection_string)
        credential = parse_credential(connection_string)
        return cls(endpoint, credential, **kwargs)
```

#### config-missing-kwargs-in-policy

```python
# Incorrect - Policy missing **kwargs
def create_config(endpoint, credential):
    return {
        "transport": RequestsTransport(),  # Missing **kwargs
        "policies": [
            UserAgentPolicy("client_name"),  # Missing **kwargs
            RetryPolicy()  # Missing **kwargs
        ]
    }

# Correct
def create_config(endpoint, credential):
    return {
        "transport": RequestsTransport(**kwargs),
        "policies": [
            UserAgentPolicy("client_name", **kwargs),
            RetryPolicy(**kwargs)
        ]
    }
```

### Method Implementation Examples

#### client-method-name-no-double-underscore

```python
# Incorrect - Double underscore prefix
class BlobClient:
    def __get_headers(self):
        return {"Content-Type": "application/json"}

# Correct - Single underscore for non-public methods
class BlobClient:
    def _get_headers(self):
        return {"Content-Type": "application/json"}
```

#### specify-parameter-names-in-call

```python
# Incorrect - Multiple positional arguments without names
def process_blob(self, container_name, blob_name, content_type, metadata, timeout):
    self._client.create_blob(container_name, blob_name, content_type, metadata, timeout)

# Correct - Named parameters for clarity
def process_blob(self, container_name, blob_name, content_type, metadata, timeout):
    self._client.create_blob(
        container_name, 
        blob_name, 
        content_type=content_type, 
        metadata=metadata, 
        timeout=timeout
    )
```

#### unapproved-client-method-name-prefix

```python
# Incorrect - Uses non-standard verb prefix
class BlobClient:
    def fetch_blob(self, name: str) -> bytes:
        pass
    
    def modify_blob(self, name: str, data: bytes) -> None:
        pass

# Correct - Uses approved verb prefixes
class BlobClient:
    def get_blob(self, name: str) -> bytes:
        pass
    
    def update_blob(self, name: str, data: bytes) -> None:
        pass
```

### Return Type Examples

#### delete-operation-wrong-return-type

```python
# Incorrect - Delete method returns a value
def delete_blob(self, name: str, **kwargs) -> Dict[str, Any]:
    response = self._client.send_request(...)
    return response.json()

# Correct - Delete method returns None
def delete_blob(self, name: str, **kwargs) -> None:
    self._client.send_request(...)
    # No return value
```

#### client-list-methods-use-paging

```python
# Incorrect - List method returns a direct list
def list_blobs(self, **kwargs) -> List[Dict[str, Any]]:
    response = self._client.send_request(...)
    return response.json()["blobs"]

# Correct - List method returns an ItemPaged instance
def list_blobs(self, **kwargs) -> ItemPaged[Dict[str, Any]]:
    def get_next(continuation_token=None):
        params = {} if continuation_token is None else {"marker": continuation_token}
        response = self._client.send_request(params=params, **kwargs)
        return response.json()
    
    return ItemPaged(get_next, extract_data=lambda r: r["blobs"])
```

### Distributed Tracing Examples

#### client-method-missing-tracing-decorator

```python
# Incorrect - Missing distributed tracing decorator
from azure.core.tracing.decorator import distributed_trace

class BlobClient:
    def get_blob(self, name: str, **kwargs) -> bytes:
        # Makes network call without tracing decorator
        return self._client.send_request(...)

# Correct
from azure.core.tracing.decorator import distributed_trace

class BlobClient:
    @distributed_trace
    def get_blob(self, name: str, **kwargs) -> bytes:
        return self._client.send_request(...)
```

#### client-method-missing-tracing-decorator-async

```python
# Incorrect - Missing async distributed tracing decorator
from azure.core.tracing.decorator_async import distributed_trace_async

class BlobClient:
    async def get_blob(self, name: str, **kwargs) -> bytes:
        # Makes network call without tracing decorator
        return await self._client.send_request(...)

# Correct
from azure.core.tracing.decorator_async import distributed_trace_async

class BlobClient:
    @distributed_trace_async
    async def get_blob(self, name: str, **kwargs) -> bytes:
        return await self._client.send_request(...)
```

### Import and Module Examples

#### no-legacy-azure-core-http-response-import

```python
# Incorrect - Imports HttpResponse from legacy location
from azure.core.pipeline.transport import HttpResponse

def process_response(response: HttpResponse) -> Dict[str, Any]:
    return response.json()

# Correct
from azure.core.rest import HttpResponse

def process_response(response: HttpResponse) -> Dict[str, Any]:
    return response.json()
```

#### do-not-import-legacy-six

```python
# Incorrect - Imports six library
import six

def is_string(s):
    return isinstance(s, six.string_types)

# Correct - Uses Python built-ins
def is_string(s):
    return isinstance(s, str)
```

#### do-not-import-asyncio

```python
# Incorrect - Direct import of asyncio functions
from asyncio import sleep
from azure.core.pipeline import AsyncPipeline
from azure.core.pipeline.transport import HttpRequest as PipelineTransportHttpRequest
from azure.core.pipeline.policies import (
    UserAgentPolicy,
    AsyncRedirectPolicy,
)
from azure.core.pipeline.transport import (
    HttpTransport,
)

async def main():
    port = 8080
    request = PipelineTransportHttpRequest("GET", "http://localhost:{}/basic/string".format(port))
    policies = [UserAgentPolicy("myusergant"), AsyncRedirectPolicy()]
    async with AsyncPipeline(HttpTransport, policies=policies) as pipeline:
        response = await pipeline.run(request)
        await sleep(0.1)
        print(response.http_response.status_code)

# Correct - Use azure-core abstractions when possible
from azure.core.pipeline import AsyncPipeline
from azure.core.pipeline.transport import HttpRequest as PipelineTransportHttpRequest
from azure.core.pipeline.policies import (
    UserAgentPolicy,
    AsyncRedirectPolicy,
)
from azure.core.pipeline.transport import (
    AsyncHttpTransport
)

async def main():
    port = 8080
    request = PipelineTransportHttpRequest("GET", "http://localhost:{}/basic/string".format(port))
    policies = [UserAgentPolicy("myusergant"), AsyncRedirectPolicy()]
    async with AsyncPipeline(AsyncHttpTransport, policies=policies) as pipeline:
        response = await pipeline.run(request)
        # Use the correct transport's sleep instead of asyncio
        transport: AsyncHttpTransport = cast(AsyncHttpTransport, response.context.transport)
        await transport.sleep(0.1)
        print(response.http_response.status_code)
```

### Enum Examples

#### enum-must-inherit-case-insensitive-enum-meta

```python
# Incorrect - Enum doesn't use CaseInsensitiveEnumMeta
from enum import Enum

class COLOR(str, Enum):
    RED = "red"
    BLUE = "blue"

# Correct
from azure.core.enum_base import CaseInsensitiveEnumMeta
from enum import Enum

class COLOR(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    RED = "red"
    BLUE = "blue"
```

### Documentation Examples

#### docstring-type-do-not-use-class

```python
# Incorrect - Using :class: in docstring type 
def get_client_by_type(self, client_type):
    """Get a client by type.
    
    :param client_type: The type of client.
    :type client_type: :class:`ClientType`
    :return: The client instance.
    :rtype: :class:`~azure.client.base.BaseClient`
    """
    pass

# Correct - Direct reference to type
def get_client_by_type(self, client_type):
    """Get a client by type.
    
    :param client_type: The type of client.
    :type client_type: ClientType
    :return: The client instance.
    :rtype: ~azure.client.base.BaseClient
    """
    pass
```

#### docstring-admonition-needs-newline

```python
# Incorrect - Missing newline before directive
def get_examples():
    """Get examples for the API.
    .. literalinclude:: ../samples/example.py
        :start-after: [START get_examples]
        :end-before: [END get_examples]
        :language: python
        :dedent: 4
    """
    pass

# Correct - Has newline before directive
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

### Error and Safety Examples

#### do-not-hardcode-connection-verify

```python
# Incorrect - Connection verify hardcoded to boolean
from azure.core.pipeline.transport import RequestsTransport

transport = RequestsTransport(connection_verify=False)  # Security risk!

# Correct - Allow user to configure or default to True
from azure.core.pipeline.transport import RequestsTransport

# Option 1: Default to secure
transport = RequestsTransport()  # Defaults to connection_verify=True

# Option 2: Accept from configuration
def create_transport(verify_ssl=True):
    return RequestsTransport(connection_verify=verify_ssl)
```

#### do-not-log-exceptions

```python
# Incorrect - Logging exception at error level
import logging
logger = logging.getLogger(__name__)

try:
    # Some operation
    response = client.get_blob("name")
except Exception as e:
    # This could log sensitive information
    logger.error("Error accessing blob: %s", str(e))
    raise

# Correct - Log at debug level or only log safe information
import logging
logger = logging.getLogger(__name__)

try:
    # Some operation
    response = client.get_blob("name")
except Exception as e:
    # Option 1: Log at debug level
    logger.debug("Error accessing blob: %s", str(e))
    
    # Option 2: Log only safe information at higher levels
    logger.error("Error accessing blob. Check debug logs for details.")
    
    raise
```