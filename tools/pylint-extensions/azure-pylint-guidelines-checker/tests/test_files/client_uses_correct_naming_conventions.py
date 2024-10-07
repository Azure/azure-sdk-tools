# test_ignores_constructor
class SomeClient():  # @
    def __init__(self, **kwargs):  # @
        pass


# test_ignores_internal_client
class _BaseSomeClient():  # @
    def __init__(self, **kwargs):  # @
        pass


# test_ignores_private_method
class Some1Client():  # @
    def _private_method(self, **kwargs):  # @
        pass

    async def _another_private_method(self, **kwargs):  # @
        pass


# test_ignores_correct_client
class Some2Client():  # @
    pass


# test_ignores_non_client
class SomethingElse():  # @
    def download_thing(self, some, **kwargs):  # @
        pass


# test_ignores_correct_method_names
class Some3Client():  # @
    def from_connection_string(self, **kwargs):  # @
        pass

    def get_thing(self, **kwargs):  # @
        pass

    def delete_thing(self, **kwargs):  # @
        pass


# test_ignores_correct_method_names_async
class Some4Client():  # @
    def from_connection_string(self, **kwargs):  # @
        pass

    def get_thing(self, **kwargs):  # @
        pass

    def delete_thing(self, **kwargs):  # @
        pass


# test_ignores_correct_class_constant
class Some5Client():  # @
    MAX_SIZE = 14
    MIN_SIZE = 2


# test_finds_incorrectly_named_client
class some_client():  # @
    pass


class Some_Client():  # @
    pass


class someClient():  # @
    pass


# test_finds_incorrectly_named_methods
class Some6Client():  # @
    def Create_Config(self):  # @
        pass

    def getThing(self):  # @
        pass

    def List_thing(self):  # @
        pass

    def UpsertThing(self):  # @
        pass

    def set_Thing(self):  # @
        pass

    def Updatething(self):  # @
        pass


# test_finds_incorrectly_named_methods_async
class Some7Client():  # @
    async def Create_Config(self):  # @
        pass

    async def getThing(self):  # @
        pass

    async def List_thing(self):  # @
        pass

    async def UpsertThing(self):  # @
        pass

    async def set_Thing(self):  # @
        pass

    async def Updatething(self):  # @
        pass


# test_finds_incorrectly_named_class_constant
class Some8Client():  # @
    max_size = 14  # @
    min_size = 2  # @


# test_ignores_docstrings
class Some9Client():  # @
    __doc__ = "Some docstring"  # @
