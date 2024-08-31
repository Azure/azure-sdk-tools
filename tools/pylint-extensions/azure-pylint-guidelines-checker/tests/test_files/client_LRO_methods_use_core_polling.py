from azure.core.polling import LROPoller
from azure.core.tracing.decorator import distributed_trace


# test_ignores_private_methods
class SomeClient():  # @
    def _begin_thing(self):  # @
        pass


# test_ignores_non_client_methods
class SomethingElse():  # @
    def begin_things(self):  # @
        pass


# test_ignores_methods_return_LROPoller
class Some1Client():  # @
    def begin_thing(self):  # @
        return LROPoller()

    @distributed_trace
    def begin_thing2(self):  # @
        return LROPoller(self._client, raw_result, get_long_running_output, polling_method)


# test_finds_method_returning_something_else
class Some2Client():  # @
    def begin_thing(self):  # @
        return list()

    def begin_thing2(self):  # @
        return {}
