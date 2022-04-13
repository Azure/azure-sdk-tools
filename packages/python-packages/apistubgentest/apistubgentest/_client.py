from datetime import datetime
from azure.core.paging import ItemPaged
from typing import Optional, Union, List, Any

from .models import FakeObject, FakeError, PetEnumPy3Metaclass


class DefaultValuesClient:

    def with_simple_default(name: str = "Bill", *, age: int = 21):
        pass

    def with_simple_optional_defaults(name: Optional[str] = "Bill", *, age: Optional[int] = 21):
        pass

    def with_falsy_optional_defaults(*, string: Optional[str] = "", int: Optional[int] = 0, bool: Optional[bool] = False):
        pass

    def with_falsy_optional_defaults_and_docstring(*, string: Optional[str] = "", int: Optional[int] = 0, bool: Optional[bool] = False):
        """ Adds the docstring, which exposes issues.
        
        :keyword str string: String. Default value is "".
        :keyword int int: Int. Default value is 0.
        :keyword bool bool: Bool. Default value is False.
        """
        pass


    def with_optional_none_defaults(name: Optional[str] = None, *, age: Optional[int] = None):
        pass

    def with_class_default(my_class: Any = FakeObject):
        pass

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
        """
        pass

    def with_enum_defaults(enum1: Union[PetEnumPy3Metaclass, str] = "DOG", enum2: Union[PetEnumPy3Metaclass, str] = PetEnumPy3Metaclass.DOG):
        pass


class Python3TypeHintClient:

    def with_simple_typehints(self, name: str, age: int) -> str:
        pass

    def with_complex_typehints(self,
        value: List[ItemPaged[Union[FakeObject, FakeError]]]  # pylint: disable=line-too-long
    ):
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
        """
        pass

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


class SpecialArgsClient:

    def with_standard_names(self, *args, **kwargs):
        pass

    def with_nonstandard_names(self, *vars, **kwds):
        pass

    def with_no_args():
        pass

    def with_keyword_only_args(self, *, value, **kwargs):
        pass

    def with_positional_only_args(self, a, b, /, c):
        pass

    def with_sorted_kwargs(self, *, d, c, b, a, **kwargs):
        pass
