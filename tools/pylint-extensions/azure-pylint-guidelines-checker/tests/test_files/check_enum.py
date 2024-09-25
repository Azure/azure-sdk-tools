from enum import Enum
from six import with_metaclass
from azure.core import CaseInsensitiveEnumMeta


# test_ignore_normal_class
class SomeClient(object):
    my_list = []


# test_enum_capitalized_violation_python_two
class MyBadEnum(with_metaclass(CaseInsensitiveEnumMeta, str, Enum)):
    One = "one"


# test_enum_capitalized_violation_python_three
class MyBadEnum2(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    One = "one"


# test_inheriting_case_insensitive_violation
class MyGoodEnum(str, Enum):
    ONE = "one"


# test_acceptable_python_three
class MyGoodEnum2(str, Enum, metaclass=CaseInsensitiveEnumMeta):
    ONE = "one"
