# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------
from typing import overload


# test_docstring_overload_implementation_args_kwargs - should NOT flag args/kwargs
class MyClient:
    @overload
    def do_thing(self, x: str, *, option: str = "default") -> str:
        ...

    @overload
    def do_thing(self, x: int, *, flag: bool = False) -> int:
        ...

    def do_thing(self, *args, **kwargs):
        """Does a thing.

        :param x: The input value.
        :type x: str or int
        """
        return None


# test_docstring_overload_non_impl_still_checks - overload-decorated function should still check
class AnotherClient:
    @overload
    def process(self, x: str) -> str:
        """Process a string.

        :param str x: The input value.
        :return: The processed value.
        :rtype: str
        """
        ...

    @overload
    def process(self, x: int) -> int:
        """Process an integer.

        :param int x: The input value.
        :return: The processed value.
        :rtype: int
        """
        ...

    def process(self, *args, **kwargs):
        """Process something.

        :param x: The input value.
        :type x: str or int
        """
        pass


# test_docstring_no_overload_still_checks_args - regular function with *args should still be checked
def regular_function(*args):
    """Does something.

    :param args: The arguments.
    :type args: Any
    """
    pass
