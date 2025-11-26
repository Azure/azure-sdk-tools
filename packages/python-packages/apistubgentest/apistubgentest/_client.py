from datetime import datetime
from azure.core.paging import ItemPaged
from typing import Optional, Union, List, Any, overload
from enum import Enum

from .models import FakeObject, FakeError, PetEnumPy3Metaclass

from azure.core import PipelineClient
from azure.core.tracing.decorator import distributed_trace
from typing import Optional, Union, overload



@overload
def module_func(a: int, *, b: str, **kwargs) -> bool:
    ...

@overload
def module_func(a: int, *, b: int, **kwargs) -> bool:
    ...

def module_func(*args, **kwargs) -> bool:
    pass

@overload
def another_func(*, b: str) -> bool:
    ...

@overload
def another_func(*, b: int) -> bool:
    ...

def another_func(*, b: Union[int, str]) -> bool:
    pass

# pylint:disable=docstring-missing-return,docstring-missing-rtype
class DefaultValuesClient:

    def with_simple_default(name: str = "Bill", *, age: int = 21) -> None:
        pass

    def with_simple_optional_defaults(name: Optional[str] = "Bill", *, age: Optional[int] = 21) -> None:
        pass

    def with_falsy_optional_defaults(*, string: Optional[str] = "", int: Optional[int] = 0, bool: Optional[bool] = False) -> None:
        pass

    def with_falsy_optional_defaults_and_docstring(*, string: Optional[str] = "", int: Optional[int] = 0, bool: Optional[bool] = False) -> None:
        """ Adds the docstring, which exposes issues.

        :keyword str string: String. Default value is "".
        :keyword int int: Int. Default value is 0.
        :keyword bool bool: Bool. Default value is False.
        """
        pass

    def with_optional_none_defaults(name: Optional[str] = None, *, age: Optional[int] = None) -> None:
        pass

    def with_class_default(my_class: Any = FakeObject) -> None:
        pass

    # pylint:disable=client-method-missing-type-annotations
    def with_parsed_docstring_defaults(name, age, some_class):
        """ Parsed docstring defaults.

        :param name: Some dummy value, defaults
        to "Bill". Extra text.
        :type name: str
        :param age: Something else, defaults
        to 21. Extra text.
        :type age: int
        :param some_class: Some kind of class type, defaults to :py:class:`apistubgen.test.models.FakeObject`.
        :type some_class: class
        :rtype: None
        """
        pass

    def with_enum_defaults(enum1: Union[PetEnumPy3Metaclass, str] = "DOG", enum2: Union[PetEnumPy3Metaclass, str] = PetEnumPy3Metaclass.DOG) -> None:
        pass


# pylint:disable=docstring-missing-return,docstring-missing-rtype
class Python3TypeHintClient:

    def with_simple_typehints(self, name: str, age: int) -> str:
        pass

    def with_complex_typehints(self,
        value: List[ItemPaged[Union[FakeObject, FakeError]]]  # pylint: disable=line-too-long
    ) -> None:
        pass

    def with_variadic_typehint(self, *vars: str, **kwargs: "Any") -> None:
        pass

    def with_str_list_return_type(self) -> List[str]:
        pass

    def with_list_return_type(self) -> List["TestClass"]:
        pass

    def with_list_union_return_type(self) -> List[Union[str, int]]:
        pass

    def with_datetime_typehint(self, date: datetime) -> datetime:
        pass


# pylint:disable=docstring-missing-return,docstring-missing-rtype
class Python2TypeHintClient:

    def with_simple_typehints(
        self,
        name, # type: str
        age # type: int
    ):
        # type: (...) -> str
        pass

    def with_complex_typehints(self,
        value # type: List[ItemPaged[Union[FakeObject, FakeError]]]  # pylint: disable=line-too-long
    ):
        # type: (...) -> None
        pass

    def with_variadic_typehint(
        self,
        *vars, # type: str
        **kwargs # type: Any
    ):
        # type: (*str, **Any) -> None
        pass

    def with_str_list_return_type(self):
        # type: (...) -> List[str]
        pass

    def with_list_return_type(self):
        # type: (...) -> List[TestClass]
        pass

    def with_list_union_return_type(self):
        # type: (...) -> List[Union[str, int]]
        pass

    def with_datetime_typehint(
        self,
        date # type: datetime
    ):
        # type: (...) -> datetime
        pass


# pylint:disable=client-method-missing-type-annotations,docstring-missing-return,docstring-missing-rtype,docstring-keyword-should-match-keyword-only
class DocstringTypeHintClient:
    def with_simple_typehints(self, name, age):
        """ Simple typehints

        :param str name: Name
        :param int age: Age
        :rtype: str
        """
        pass

    def with_complex_typehints(self, value):
        """ Complex typehint
        :param value: Value
        :type value: List[ItemPaged[Union[FakeObject, FakeError]]]
        :rtype: None
        """
        pass

    # pylint:disable=docstring-should-be-keyword
    def with_variadic_typehint(self, *vars, **kwargs):
        """ Variadic typehint
        :param str vars: Args
        :param Any kwargs: Kwargs
        :rtype: None
        """
        pass

    def with_str_list_return_type(self):
        """" String list return

        :rtype: List[str]
        """
        pass

    def with_list_return_type(self):
        """" String list return

        :rtype: List[TestClass]
        """
        pass

    def with_list_union_return_type(self):
        """" List union return

        :rtype: List[Union[str, int]]
        """
        pass

    def with_datetime_typehint(self, date):
        """ With datetime

        :param datetime date: Datetime
        :rtype: datetime
        """
        pass

    def with_explicit_kwargs_docstring(self, **kwargs: Any) -> str:
        """ With kwargs

        :keyword Any \\**kwargs: Optional parameters.
        :rtype: str
        """
        pass

    def with_incorrect_param_kwargs_docstring(self, **kwargs: Any) -> str:
        """ With incorrect docstring param kwargs

        :param Any \\**kwargs: Optional parameters.
        :rtype: str
        """
        pass
    def with_incorrect_dict_kwargs_docstring(self, **kwargs: Any) -> str:
        """ With incorrect docstring param kwargs

        :keyword kwargs: Optional parameters.
        :paramtype kwargs: Any
        :rtype: str
        """
        pass

    def with_incorrect_dict_kwargs_docstring2(self, **kwargs: Any) -> str:
        """ With incorrect docstring param kwargs

        :keyword **kwargs: Optional parameters.
        :paramtype **kwargs: Any
        :rtype: str
        """
        pass


class SpecialArgsClient:

    def with_standard_names(self, *args, **kwargs) -> None:
        pass

    def with_nonstandard_names(self, *vars, **kwds) -> None:
        pass

    def with_no_args() -> None:
        pass

    def with_keyword_only_args(self, *, value, **kwargs) -> None:
        pass

    def with_positional_only_args(self, a, b, /, c) -> None:
        pass

    def with_sorted_kwargs(self, *, d, c, b, a, **kwargs) -> None:
        pass


class PylintCheckerViolationsClient(PipelineClient):

    def __init__(self, endpoint: str, connection_string: str):
        self.endpoint = endpoint
        self.connection_string = connection_string

    def with_too_many_args(self, a: str, b: str, c: str, d: str, e:str , f: str, g: str, h: str, **kwargs: Any) -> None:
        pass

    def without_type_annotations(self, val) -> None:
        pass

    def without_return_annotation(self, val: str):
        pass

    ### Check that no duplicate pylint errors are added on methods and properties.

    @distributed_trace
    def list_secrets(
        self,
        name,  # type: str
        **kwargs  # type: Any
    ):
        # type: (...) -> List[str]
        """List secrets with legacy type comments.

        This will trigger do-not-use-legacy-typing pylint error.
        """
        pass

    @distributed_trace
    def describe_secret(
        self,
        id,  # type: str
    ):
        # type: (...) -> str
        """Describe a secret with legacy type comments.

        This will trigger do-not-use-legacy-typing pylint error.
        """
        pass

    @overload
    def get_secret(self, name: str, *, version: str, **kwargs) -> str:
        """Get a secret by name and version.

        :param name: The secret name
        :type name: str
        :keyword version: The secret version
        :paramtype version: str
        :return: The secret value
        :rtype: str
        """
        ...

    @overload
    def get_secret(self, name: str, **kwargs) -> str:
        """Get a secret by name.

        :param name: The secret name
        :type name: str
        :return: The secret value
        :rtype: str
        """
        ...

    @distributed_trace
    def get_secret(self, name: str, *, version: Optional[str] = None, **kwargs) -> str:
        """Get a secret.

        :param name: The secret name
        :type name: str
        :keyword version: The secret version
        :paramtype version: str
        :return: The secret value
        :rtype: str
        """
        pass


# Enum that doesn't inherit from CaseInsensitiveEnumMeta
class PylintViolationEnum(str, Enum):
    """Enumeration that will trigger enum-must-inherit-case-insensitive-enum-meta."""
    PASSWORD = "password"
    CERTIFICATE = "certificate"
