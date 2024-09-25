from azure.core.tracing.decorator import distributed_trace
from azure.core.tracing.decorator_async import distributed_trace_async


class SomeClient():  # @
    # test_ignores_repr
    def __repr__(self):  # @
        pass

    # test_ignores_constructor
    def __init__(self, **kwargs):  # @
        pass

    # test_ignores_other_dunder
    def __enter__(self):  # @
        pass

    def __exit__(self):  # @
        pass

    def __aenter__(self):  # @
        pass

    def __aexit__(self):  # @
        pass

    # test_ignores_private_method
    @staticmethod
    def _private_method(self, **kwargs):  # @
        pass

    # test_ignores_private_method_async
    @staticmethod
    async def _private_method(self, **kwargs):  # @
        pass

    # test_ignores_methods_with_decorators
    @distributed_trace
    def create_configuration(self):  # @
        pass

    @distributed_trace
    def get_thing(self):  # @
        pass

    @distributed_trace
    def list_thing(self):  # @
        pass

    # test_ignores_async_methods_with_decorators
    @distributed_trace_async
    async def create_configuration(self):  # @
        pass

    @distributed_trace_async
    async def get_thing(self):  # @
        pass

    @distributed_trace_async
    async def list_thing(self):  # @
        pass


# test_finds_double_underscore_on_async_method
class Some1Client():  # @
    @staticmethod
    async def __create_configuration(self):  # @
        pass

    @staticmethod
    async def __get_thing(self):  # @
        pass

    @staticmethod
    async def __list_thing(self):  # @
        pass


# test_finds_double_underscore_on_sync_method
class Some2Client():
    @staticmethod
    def __create_configuration(self):  # @
        pass

    @staticmethod
    def __get_thing(self):  # @
        pass

    @staticmethod
    def __list_thing(self):  # @
        pass


# test_ignores_non_client_method
class SomethingElse():  # @
    @staticmethod
    def __download_thing(self, some, **kwargs):  # @
        pass

    @staticmethod
    async def __do_thing(self, some, **kwargs):  # @
        pass
