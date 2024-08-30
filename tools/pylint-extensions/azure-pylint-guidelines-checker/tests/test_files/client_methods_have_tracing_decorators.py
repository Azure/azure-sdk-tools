from azure.core.tracing import decorator
from azure.core.tracing.decorator import distributed_trace
from azure.core.tracing.decorator_async import distributed_trace_async


class SomeClient():  # @
    def __init__(self, **kwargs):  # @
        pass

    def _private_method(self, **kwargs):  # @
        pass

    async def _private_method2(self, **kwargs):  # @
        pass

    @distributed_trace
    def create_configuration(self, **kwargs):  # @
        pass

    @distributed_trace
    def get_thing(self, **kwargs):  # @
        pass

    @distributed_trace
    def list_thing(self, **kwargs):  # @
        pass

    @distributed_trace_async
    async def create_configuration(self, **kwargs):  # @
        pass

    @distributed_trace_async
    async def get_thing(self, **kwargs):  # @
        pass

    @distributed_trace_async
    async def list_thing(self, **kwargs):  # @
        pass

    @distributed_trace
    async def create_configuration(self, **kwargs):  # @
        pass

    @distributed_trace
    async def get_thing(self, **kwargs):  # @
        pass

    @distributed_trace
    async def list_thing(self, **kwargs):  # @
        pass

    @distributed_trace_async
    def create_configuration(self, **kwargs):  # @
        pass

    @distributed_trace_async
    def get_thing(self, **kwargs):  # @
        pass

    @distributed_trace_async
    def list_thing(self, **kwargs):  # @
        pass

    @classmethod
    @distributed_trace
    def download_thing(self, some, **kwargs):  # @
        pass

    @distributed_trace
    @decorator
    def do_thing(self, some, **kwargs):  # @
        pass

    @classmethod
    @distributed_trace_async
    async def download_thing(self, some, **kwargs):  # @
        pass

    @distributed_trace_async
    @decorator
    async def do_thing(self, some, **kwargs):  # @
        pass


class SomethingElse():  # @
    def download_thing(self, some, **kwargs):  # @
        pass

    @classmethod
    async def do_thing(self, some, **kwargs):  # @
        pass
