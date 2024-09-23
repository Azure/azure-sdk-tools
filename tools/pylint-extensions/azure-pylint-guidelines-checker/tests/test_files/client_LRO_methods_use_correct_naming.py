from azure.core.polling import LROPoller
from azure.core.tracing.decorator import distributed_trace


class SomeClient():  # @
    def _do_thing(self):
        return LROPoller(self._client, raw_result, get_long_running_output, polling_method)  # @


# test_ignores_non_client_methods
class SomethingElse():  # @
    def begin_things(self):
        return LROPoller(self._client, raw_result, get_long_running_output, polling_method)  # @


# test_ignores_methods_return_LROPoller_and_correctly_named
class Some1Client():  # @
    def begin_thing(self):
        return LROPoller()  # @

    @distributed_trace
    def begin_thing2(self):
        return LROPoller(self._client, raw_result, get_long_running_output, polling_method)  # @


# test_finds_incorrectly_named_method_returning_LROPoller
class Some2Client():  # @
    def poller_thing(self):  # @
        return LROPoller()  # @

    @distributed_trace
    def start_thing2(self):  # @
        return LROPoller(self._client, raw_result, get_long_running_output, polling_method)  # @

