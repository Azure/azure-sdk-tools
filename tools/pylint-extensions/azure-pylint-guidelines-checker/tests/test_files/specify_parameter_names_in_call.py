class SomeClient():  # @
    # test_ignores_call_with_only_two_unnamed_params
    def do_thing(self):
        self._client.thing(one, two)  # @

    # test_ignores_call_with_two_unnamed_params_and_one_named
    def do_1thing(self):
        self._client.thing(one, two, three=3)  # @


# test_ignores_call_from_non_client
class SomethingElse():  # @
    def do_thing(self):
        self._other.thing(one, two, three)  # @


# test_ignores_call_with_named_params
class SomethingElse1(): #@
    def do_thing_a(self):
        self._other.thing(one=one, two=two, three=three)  # @

    def do_thing_b(self):
        self._other.thing(zero, number, one=one, two=two, three=three)  # @

    def do_thing_c(self):
        self._other.thing(zero, one=one, two=two, three=three)  # @


# test_ignores_non_client_function_call
class TestIgnoresNonClientFunctionCall():  # @
    def do_thing(self):
        self._client.thing(one, two, three)  # @


# test_finds_call_with_more_than_two_unnamed_params
class Some1Client():  # @
    def do_thing(self):
        self._client.thing(one, two, three)  # @


# test_finds_call_with_more_than_two_unnamed_params_and_some_named
class Some2Client():  # @
    def do_thing(self):
        self._client.thing(one, two, three, four=4, five=5)  # @




