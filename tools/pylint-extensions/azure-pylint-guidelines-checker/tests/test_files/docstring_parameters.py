# test_docstring_vararg
def function_foo(x, y, *z):
    """
    :param x: x
    :type x: str
    :param str y: y
    :param str z: z
    """


# test_docstring_vararg_keyword_args
def function_foo(x, y, *z, a="Hello", b="World"):
    """
    :param x: x
    :type x: str
    :param str y: y
    :param str z: z
    :keyword str a: a
    :keyword str b: b
    """


# test_docstring_varag_no_type
def function_foo(*x):
    """
    :param x: x
    :keyword z: z
    :paramtype z: str
    """


# test_docstring_class_paramtype
class MyClass():  # @
    def function_foo(**kwargs):  # @
        """
        :keyword z: z
        :paramtype z: str
        """

    def function_boo(**kwargs):  # @
        """
        :keyword z: z
        :paramtype z: str
        """


from typing import Dict


# test_docstring_property_decorator
@property
def function_foo(self) -> Dict[str, str]:
    """The current headers collection.
        :rtype: dict[str, str]
    """
    return {"hello": "world"}


# test_docstring_no_property_decorator
def function_foo(self) -> Dict[str, str]:
    """The current headers collection.
    :rtype: dict[str, str]
    """
    return {"hello": "world"}


# test_docstring_type_has_space
def function_foo(x):
    """
    :param dict[str, int] x: x
    """


# test_docstring_type_has_many_spaces
def function_foo(x):
    """
    :param  dict[str, int]  x: x
    """


# test_docstring_raises
def function_foo():
    """
    :raises: ValueError
    """
    print("hello")
    raise ValueError("hello")


# test_docstring_keyword_only
def function_foo(self, x, *, z, y=None):
    '''
    :param x: x
    :type x: str
    :keyword str y: y
    :keyword str z: z
    '''
    print("hello")


# test_docstring_correct_rtype
def function_foo(self, x, *, z, y=None) -> str:
    """
    :param x: x
    :type x: str
    :keyword str y: y
    :keyword str z: z
    :rtype: str
    """
    print("hello")


# test_docstring_class_type
def function_foo(self, x, y):
    """
    :param x: x
    :type x: :class:`azure.core.credentials.AccessToken`
    :param y: y
    :type y: str
    :rtype: :class:`azure.core.credentials.AccessToken`
    """
    print("hello")
