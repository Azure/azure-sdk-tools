## Python APIView Error Guidance

This document contains amplifying information on errors raised by the Python APIView tool
(aka api-stub-generator). 

### decorator-parse

If this error appears please file an issue in the [Azure SDK Tools repo](https://github.com/Azure/azure-sdk-tools/issues) with a link to the APIView that contains the error.

### list-return-type

List APIs should return either an `ItemPaged` or `AsyncItemPaged` object, and the item type must be specified in the `rtype` annotation in the docstring.

**Good**
```python
:rtype: ~azure.core.paging.ItemPaged[~azure.mgmt.sql.models.MetricListResult]
```

### missing-kwargs

Most public methods in our SDKs should expose `**kwargs` as part of the signature. If you feel this requirement should not apply to your
situation, discuss with the Azure SDK team and, if approved, suppress this error.

**Bad**
```python
def get(self, name):
    pass
```

**Good**
```python
def get(self, name, **kwargs):
    pass
```

### missing-return-type

All methods must specify their return type. There are three ways to specify return type, Python 2 or 3-style typehints or docstring annotations.
Because the Azure SDK no longer supports Python 2, Python 2-style typehints are discouraged. Python 3-style typehints are preferred.

**Python 3 Typehint (recommended)**
```python
def get(self, name: str) -> MyObject:
    pass
```

**Docstring Annotation**
```python
def get(self, name):
    """ Get an object.
    :rtype: ~azure.myservice.models.MyObject
    """
    pass
```

**Python 2 Typehint (NOT recommended)**
```python
def get(self, name: str) -> MyObject:
    # type: (...) -> MyObject
    pass
```

### missing-source-link

The specified type is not part of the package the APIView is generated for. If this is a type from a dependency,
ensure you provide the fully qualified name.

**Bad**
```python
:rtype: ItemPaged[MyObject]
```

**Good**
```python
:rtype: ~azure.core.paging.ItemPaged[MyObject]
```

### missing-type

Type information is not available for the specified argument. Python APIView only supports Python 3-style typehints and docstring annotations
for arguments. Python 2-style typehints are **NOT** parsed.

**Bad**
```python
def get(
    self,
    name: # type: str
):
    pass
```

**Good (Python 3)**
```python
def get(self, name: str):
    pass
```

**Good (Docstring)**
```python
def get(self, name):
    """ Get an object.

    :param str name: The object name
    """
    pass
```

### missing-typehint

A typehint is required for this method but has not been provided.

### name-mismatch

This error arises when a model name is aliased to some other name in an incorrect way. 

**Bad**
```python
from ._generated.models import SomePoorlyNamedThing as SomeBetterNamedThing

__all__ = [
    "SomeBetterNamedThing"
]
```

Doing just this will result in inconsistent naming in tools like Sphinx. You need to also set `__name__` to match.

**Good**
```python
from ._generated.models import SomePoorlyNamedThing as SomeBetterNamedThing

SomeBetterNamedThing.__name__ = "SomeBetterNamedThing"

__all__ = [
    "SomeBetterNamedThing"
]
```

### return-type-mismatch

This error occurs when you have specified both a typehint and a docstring return type, but the types do not match.

**Bad**
```python
def send_request(self, request):
    # type: (...) -> HTTPResponseType
    """Method that runs the network request through the client's chained policies.

    :return: The response of your network call. Does not do error handling on your response.
    :rtype: ~azure.core.rest.HttpResponse
    """
```

**Good**
```python
def send_request(self, request):
    # type: (...) -> HttpResponse
    """Method that runs the network request through the client's chained policies.

    :return: The response of your network call. Does not do error handling on your response.
    :rtype: ~azure.core.rest.HttpResponse
    """
```