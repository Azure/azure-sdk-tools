# test_ignores_correct_admonition_statement_in_function
def function_foo(x, y, z):
    """docstring
    .. admonition:: Example:

        .. literalinclude:: ../samples/sample_detect_language.py
    """
    pass


# test_ignores_correct_admonition_statement_in_function_with_comments
def function_foo1(x, y, z):
    """docstring
    .. admonition:: Example:
        This is Example content.
        Should support multi-line.
        Can also include file:

        .. literalinclude:: ../samples/sample_detect_language.py
    """


# test_bad_admonition_statement_in_function
def function_foo2(x, y, z):
    """docstring
    .. admonition:: Example:
        .. literalinclude:: ../samples/sample_detect_language.py
    """


# test_bad_admonition_statement_in_function_with_comments
def function_foo3(x, y, z):
    """docstring
    .. admonition:: Example:
        This is Example content.
        Should support multi-line.
        Can also include file:
        .. literalinclude:: ../samples/sample_detect_language.py
    """


# test_ignores_correct_admonition_statement_in_function_async
async def function_foo4(x, y, z):
    """docstring
    .. admonition:: Example:

        .. literalinclude:: ../samples/sample_detect_language.py
    """


# test_ignores_correct_admonition_statement_in_function_with_comments_async
async def function_foo5(x, y, z):
    """docstring
    .. admonition:: Example:
        This is Example content.
        Should support multi-line.
        Can also include file:

        .. literalinclude:: ../samples/sample_detect_language.py
    """


# test_bad_admonition_statement_in_function_async
async def function_foo6(x, y, z):
    """docstring
    .. admonition:: Example:
        .. literalinclude:: ../samples/sample_detect_language.py
    """


# test_bad_admonition_statement_in_function_with_comments_async
async def function_foo7(x, y, z):
    """docstring
    .. admonition:: Example:
        This is Example content.
        Should support multi-line.
        Can also include file:
        .. literalinclude:: ../samples/sample_detect_language.py
    """


# test_ignores_correct_admonition_statement_in_class
class SomeClient(object):
    """docstring
    .. admonition:: Example:

        .. literalinclude:: ../samples/sample_detect_language.py
    """

    def __init__(self):
        pass


# test_ignores_correct_admonition_statement_in_class_with_comments
class Some1Client():  # @
    """docstring
    .. admonition:: Example:
        This is Example content.
        Should support multi-line.
        Can also include file:

        .. literalinclude:: ../samples/sample_detect_language.py
    """

    def __init__(self):
        pass


# test_bad_admonition_statement_in_class
class Some2Client():  # @
    """docstring
    .. admonition:: Example:
        .. literalinclude:: ../samples/sample_detect_language.py
    """

    def __init__(self):
        pass


# test_bad_admonition_statement_in_class_with_comments
class Some3Client():  # @
    """docstring
    .. admonition:: Example:
        This is Example content.
        Should support multi-line.
        Can also include file:
        .. literalinclude:: ../samples/sample_detect_language.py
    """

    def __init__(self):
        pass
