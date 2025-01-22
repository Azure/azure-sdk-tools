# test_ignores_correct_dedent_in_function
def function_foo(x, y, z):
    """docstring
    .. admonition:: Example:

        .. literalinclude:: ../samples/sample_authentication.py 
                 :start-after: [START auth_from_connection_string] 
                 :end-before: [END auth_from_connection_string] 
                 :language: python 
                 :dedent:
                 :caption: Authenticate with a connection string
    """
    pass


# test_failure_dedent_in_function
def function_foo1(x, y, z):
    """docstring
    .. admonition:: Example:
        This is Example content.
        Should support multi-line.
        Can also include file:

        .. literalinclude:: ../samples/sample_authentication.py 
                 :start-after: [START auth_from_connection_string] 
                 :end-before: [END auth_from_connection_string] 
                 :language: python 
                 :dedent: 8 
    """


# test_ignores_correct_dedent_in_class
class SomeClient(object):
    """docstring
    .. admonition:: Example:
        .. literalinclude:: ../samples/sample_authentication.py 
                 :start-after: [START auth_from_connection_string] 
                 :end-before: [END auth_from_connection_string] 
                 :language: python 
                 :dedent:
                 :caption: Authenticate with a connection string
    """

    def __init__(self):
        pass


# test_failure_dedent_in_class
class Some1Client():  # @
    """docstring
    .. admonition:: Example:
        This is Example content.
        Should support multi-line.
        Can also include file:

        .. literalinclude:: ../samples/sample_authentication.py 
                 :start-after: [START auth_from_connection_string] 
                 :end-before: [END auth_from_connection_string] 
                 :language: python 
                 :dedent: 8 
    """

    def __init__(self):
        pass