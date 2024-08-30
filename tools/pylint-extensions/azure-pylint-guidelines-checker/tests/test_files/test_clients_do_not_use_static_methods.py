from azure.core.tracing import decorator
from azure.core.tracing.decorator import distributed_trace
from azure.core.tracing.decorator_async import distributed_trace_async


class SomeClient():  # @
    def __init__(self, **kwargs):  # @
        pass

    @staticmethod
    def _private_method(self, **kwargs):  # @
        pass

    @staticmethod
    async def _private_method(self, **kwargs):  # @
        pass

    @distributed_trace
    def create_configuration(self):  # @
        pass

    @distributed_trace
    def get_thing(self):  # @
        pass

    @distributed_trace
    def list_thing(self):  # @
        pass

    @distributed_trace_async
    async def create_configuration(self):  # @
        pass

    @distributed_trace_async
    async def get_thing(self):  # @
        pass

    @distributed_trace_async
    async def list_thing(self):  # @
        pass

    @staticmethod
    async def create_configuration2(self):  #@
        pass

    @staticmethod
    async def get_thing2(self):  #@
        pass

    @staticmethod
    async def list_thing2(self):  #@
        pass

    @staticmethod
    def create_configuration3(self):  # @
        pass

    @staticmethod
    def get_thing3(self):  # @
        pass

    @staticmethod
    def list_thing3(self):  # @
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
    @staticmethod
    def download_thing(self, some, **kwargs):  # @
        pass

    @staticmethod
    async def do_thing(self, some, **kwargs):  # @
        pass
