from azure.core.tracing import decorator
from azure.core.tracing.decorator import distributed_trace
from azure.core.tracing.decorator_async import distributed_trace_async


class SomeClient():  # @
    # test_ignores_constructor
    def __init__(self, **kwargs):  # @
        pass

    # test_ignores_private_method
    @staticmethod
    def _private_method(self, **kwargs):  # @
        pass

    # test_ignores_private_method_async
    @staticmethod
    async def _private_method(self, **kwargs):  # @
        pass

    # test_ignores_methods_with_other_decorators
    @distributed_trace
    def create_configuration(self):  # @
        pass

    @distributed_trace
    def get_thing(self):  # @
        pass

    @distributed_trace
    def list_thing(self):  # @
        pass

    # test_ignores_async_methods_with_other_decorators
    @distributed_trace_async
    async def create_configuration(self):  # @
        pass

    @distributed_trace_async
    async def get_thing(self):  # @
        pass

    @distributed_trace_async
    async def list_thing(self):  # @
        pass

    # test_finds_staticmethod_on_async_method
    @staticmethod
    async def create_configuration2(self):  #@
        pass

    @staticmethod
    async def get_thing2(self):  #@
        pass

    @staticmethod
    async def list_thing2(self):  #@
        pass

    # test_finds_staticmethod_on_sync_method
    @staticmethod
    def create_configuration3(self):  # @
        pass

    @staticmethod
    def get_thing3(self):  # @
        pass

    @staticmethod
    def list_thing3(self):  # @
        pass

    # test_ignores_other_multiple_decorators
    @classmethod
    @distributed_trace
    def download_thing(self, some, **kwargs):  # @
        pass

    @distributed_trace
    @decorator
    def do_thing(self, some, **kwargs):  # @
        pass

    # test_ignores_other_multiple_decorators_async
    @classmethod
    @distributed_trace_async
    async def download_thing(self, some, **kwargs):  # @
        pass

    @distributed_trace_async
    @decorator
    async def do_thing(self, some, **kwargs):  # @
        pass


# test_ignores_non_client_method
class SomethingElse():  # @
    @staticmethod
    def download_thing(self, some, **kwargs):  # @
        pass

    @staticmethod
    async def do_thing(self, some, **kwargs):  # @
        pass
