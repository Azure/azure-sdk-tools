class SomeClient():  # @
    # test_finds_correct_params
    def __init__(self, thing_url, credential, **kwargs):  # @
        pass

    # test_ignores_non_constructor_methods
    def create_configuration(self):  # @
        pass


# test_ignores_non_client_constructor_methods
class SomethingElse():  # @
    def __init__(self):  # @
        pass


# test_finds_constructor_without_kwargs
class Some1Client():  # @
    def __init__(self, thing_url, credential=None):  # @
        pass


# test_finds_constructor_without_credentials
class Some2Client():  # @
    def __init__(self, thing_url, **kwargs):  # @
        pass


# test_finds_constructor_with_no_params
class Some3Client():  # @
    def __init__(self):  # @
        pass
