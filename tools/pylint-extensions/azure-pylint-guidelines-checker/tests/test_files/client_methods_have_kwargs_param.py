from azure.core.tracing.decorator import distributed_trace
from azure.core.tracing.decorator_async import distributed_trace_async


# test_ignores_private_methods
class SomeClient():  # @
    def _create_configuration(self):  # @
        pass


# test_ignores_properties
class Some1Client():  # @
    @property
    def key_id(self):  # @
        pass


# test_ignores_properties_async
class Some2Client():  # @
    @property
    async def key_id(self):  # @
        pass


# test_ignores_non_client_methods
class Some3Client():  # @
    def create_configuration(self):  # @
        pass


# test_ignores_methods_with_kwargs
class Some4Client():  # @
    def get_thing(self, **kwargs):  # @
        pass

    @distributed_trace
    def remove_thing(self, **kwargs):  # @
        pass


# test_finds_missing_kwargs
class Some5Client():  # @
    @distributed_trace
    def get_thing(self):  # @
        pass

    @distributed_trace
    def remove_thing(self):  # @
        pass


# test_ignores_methods_with_kwargs_async
class Some6Client():  # @
    async def get_thing(self, **kwargs):  # @
        pass

    async def remove_thing(self, **kwargs):  # @
        pass


# test_finds_missing_kwargs_async
class Some7Client():  # @
    @distributed_trace_async
    async def get_thing(self):  # @
        pass

    @distributed_trace_async
    async def remove_thing(self):  # @
        pass
