class _AsyncBaseSomeClient():  # @
    # test_ignores_private_client
    def create_configuration(self):
        pass


# test_ignores_correct_client
class SomeClient():  # @
    def create_configuration(self):  # @
        pass


# test_ignores_async_base_named_client
class AsyncSomeClientBase():  # @
    def get_thing(self, **kwargs):
        pass


# test_finds_incorrectly_named_client
class AsyncSomeClient():  # @
    def get_thing(self, **kwargs):
        pass


# test_ignores_non_client
class SomethingElse():  # @
    def create_configuration(self):  # @
        pass
