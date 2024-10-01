from typing import Union


class SomeClient():  # @
    # test_ignores_correct_type_annotations
    def do_thing(self, one: str, two: int, three: bool, four: Union[str, thing], five: dict) -> int:  # @
        pass

    async def do_thing(self, one: str, two: int, three: bool, four: Union[str, thing], five: dict) -> int:  # @
        pass

    # test_ignores_correct_type_comments
    def do_thing_a(self, one, two, three, four, five):  # @
        # type: (str, str, str, str, str) -> None
        pass

    def do_thing_b(self, one, two):  # type: (str, str) -> int #@
        pass

    def do_thing_c(self,  # @
                   one,  # type: str
                   two,  # type: str
                   three,  # type: str
                   four,  # type: str
                   five  # type: str
                   ):
        # type: (...) -> int
        pass

    # test_ignores_correct_type_comments_async
    async def do_thing_a(self, one, two, three, four, five):  # @
        # type: (str, str, str, str, str) -> None
        pass

    async def do_thing_b(self, one, two):  # type: (str, str) -> int #@
        pass

    async def do_thing_c(self,  # @
                         one,  # type: str
                         two,  # type: str
                         three,  # type: str
                         four,  # type: str
                         five  # type: str
                         ):
        # type: (...) -> int
        pass

    # test_ignores_no_parameter_method_with_annotations
    def do_thing_a(self):  # @
        # type: () -> None
        pass

    def do_thing_b(self) -> None:  # @
        pass

    # test_ignores_no_parameter_method_with_annotations_async
    async def do_thing_a(self):  # @
        # type: () -> None
        pass

    async def do_thing_b(self) -> None:  # @
        pass


# test_finds_no_parameter_method_without_annotations
class Some1Client():  # @
    def do_thing(self):  # @
        pass

    async def do_thing(self):  # @
        pass


# test_finds_method_missing_annotations
class Some2Client():  # @
    def do_thing(self, one, two, three):  # @
        pass


# test_finds_method_missing_annotations_async
class Some3Client():  # @
    async def do_thing(self, one, two, three):  # @
        pass


# test_finds_constructor_without_annotations
class Some4Client():  # @
    def __init__(self, one, two, three, four, five):  # @
        pass


# test_finds_missing_return_annotation_but_has_type_hints
class Some5Client():  # @
    def do_thing_a(self, one: str, two: int, three: bool, four: Union[str, thing], five: dict):  # @
        pass

    def do_thing_b(self, one, two, three, four, five):  # @
        # type: (str, str, str, str, str)
        pass


# test_finds_missing_return_annotation_but_has_type_hints_async
class Some6Client():  # @
    async def do_thing_a(self, one: str, two: int, three: bool, four: Union[str, thing], five: dict):  # @
        pass

    async def do_thing_b(self, one, two, three, four, five):  # @
        # type: (str, str, str, str, str)
        pass


# test_finds_missing_annotations_but_has_return_hint
class Some7Client():  # @
    def do_thing_a(self, one, two, three, four, five) -> None:  # @
        pass

    def do_thing_b(self, one, two, three, four, five):  # @
        # type: -> None
        pass


# test_finds_missing_annotations_but_has_return_hint_async
class Some8Client():  # @
    async def do_thing_a(self, one, two, three, four, five) -> None:  # @
        pass

    async def do_thing_b(self, one, two, three, four, five):  # @
        # type: -> None
        pass


class SomethingElse():  # @
    # test_ignores_non_client_methods
    def do_thing(self, one, two, three, four, five, six):  # @
        pass

    # test_ignores_private_methods
    def _do_thing(self, one, two, three, four, five, six):  # @
        pass
