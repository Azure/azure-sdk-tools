# test_ignores_client_with_no_conn_str_in_constructor
class SomeClient():  # @
    def __init__(self):
        pass


# test_ignores_non_client_methods
class SomethingElse():  # @
    def __init__(self):  # @
        pass


# test_finds_client_method_using_conn_str_in_constructor_a
class Some1Client():  # @
    def __init__(self, connection_string):
        return list()


# test_finds_client_method_using_conn_str_in_constructor_b
class Some2Client():  # @
    def __init__(self, conn_str):
        return list()
