from azure.core.tracing.decorator import distributed_trace
from azure.core.tracing.decorator_async import distributed_trace_async


# test_ignores_method_abiding_to_guidelines
class SomeClient():  # @
    @distributed_trace
    def do_thing(self):  # @
        pass

    def do_thing_a(self):  # @
        pass

    def do_thing_b(self, one):  # @
        pass

    def do_thing_c(self, one, two):  # @
        pass

    def do_thing_d(self, one, two, three):  # @
        pass

    def do_thing_e(self, one, two, three, four):  # @
        pass

    def do_thing_f(self, one, two, three, four, five):  # @
        pass

    def do_thing_g(self, one, two, three, four, five, six=6):  # @
        pass

    def do_thing_h(self, one, two, three, four, five, six=6, seven=7):  # @
        pass

    def do_thing_i(self, one, two, three, four, five, *, six=6, seven=7):  # @
        pass

    def do_thing_j(self, one, two, three, four, five, *, six=6, seven=7):  # @
        pass

    def do_thing_k(self, one, two, three, four, five, **kwargs):  # @
        pass

    def do_thing_l(self, one, two, three, four, five, *args, **kwargs):  # @
        pass

    def do_thing_m(self, one, two, three, four, five, *args, six, seven=7, **kwargs):  # @
        pass


# test_ignores_method_abiding_to_guidelines_async
class Some2Client():  # @
    @distributed_trace_async
    async def do_thing(self):  # @
        pass

    async def do_thing_a(self):  # @
        pass

    async def do_thing_b(self, one):  # @
        pass

    async def do_thing_c(self, one, two):  # @
        pass

    async def do_thing_d(self, one, two, three):  # @
        pass

    async def do_thing_e(self, one, two, three, four):  # @
        pass

    async def do_thing_f(self, one, two, three, four, five):  # @
        pass

    async def do_thing_g(self, one, two, three, four, five, six=6):  # @
        pass

    async def do_thing_h(self, one, two, three, four, five, six=6, seven=7):  # @
        pass

    async def do_thing_i(self, one, two, three, four, five, *, six=6, seven=7):  # @
        pass

    async def do_thing_j(self, one, two, three, four, five, *, six=6, seven=7):  # @
        pass

    async def do_thing_k(self, one, two, three, four, five, **kwargs):  # @
        pass

    async def do_thing_l(self, one, two, three, four, five, *args, **kwargs):  # @
        pass

    async def do_thing_m(self, one, two, three, four, five, *args, six, seven=7, **kwargs):  # @
        pass


# test_finds_methods_with_too_many_positional_args
class Some3Client():  # @
    @distributed_trace
    def do_thing(self, one, two, three, four, five, six):  # @
        pass

    def do_thing_a(self, one, two, three, four, five, six, seven=7):  # @
        pass

    def do_thing_b(self, one, two, three, four, five, six, *, seven):  # @
        pass

    def do_thing_c(self, one, two, three, four, five, six, *, seven, eight, nine):  # @
        pass

    def do_thing_d(self, one, two, three, four, five, six, **kwargs):  # @
        pass

    def do_thing_e(self, one, two, three, four, five, six, *args, seven, eight, nine):  # @
        pass

    def do_thing_f(self, one, two, three, four, five, six, *args, seven=7, eight=8, nine=9):  # @
        pass


# test_finds_methods_with_too_many_positional_args_async
class Some4Client():  # @
    @distributed_trace_async
    async def do_thing(self, one, two, three, four, five, six):  # @
        pass

    async def do_thing_a(self, one, two, three, four, five, six, seven=7):  # @
        pass

    async def do_thing_b(self, one, two, three, four, five, six, *, seven):  # @
        pass

    async def do_thing_c(self, one, two, three, four, five, six, *, seven, eight, nine):  # @
        pass

    async def do_thing_d(self, one, two, three, four, five, six, **kwargs):  # @
        pass

    async def do_thing_e(self, one, two, three, four, five, six, *args, seven, eight, nine):  # @
        pass

    async def do_thing_f(self, one, two, three, four, five, six, *args, seven=7, eight=8, nine=9):  # @
        pass


# test_ignores_non_client_methods
class SomethingElse():  # @
    def do_thing(self, one, two, three, four, five, six):  # @
        pass

    @distributed_trace_async
    async def do_thing(self, one, two, three, four, five, six):  # @
        pass
