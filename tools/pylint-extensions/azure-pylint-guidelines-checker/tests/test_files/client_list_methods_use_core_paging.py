from azure.core.paging import ItemPaged
from azure.core.async_paging import AsyncItemPaged
from azure.core.polling import LROPoller
from typing import list

from azure.core.tracing.decorator import distributed_trace


# test_ignores_private_methods
class SomeClient():  # @
    def _list_thing(self):  # @
        pass  # @


# test_ignores_non_client_methods
class SomethingElse():  # @
    def list_things(self):  # @
        pass


# test_ignores_methods_return_ItemPaged
class Some1Client():  # @
    def list_thing(self):  # @
        return ItemPaged()

    @distributed_trace
    def list_thing2(self):  # @
        return ItemPaged(
            command, prefix=name_starts_with, results_per_page=results_per_page,
            page_iterator_class=BlobPropertiesPaged)


# test_ignores_methods_return_AsyncItemPaged
class Some2Client():  # @
    async def list_thing(self):  # @
        return AsyncItemPaged()

    @distributed_trace
    def list_thing2(self):  # @
        return AsyncItemPaged(
            command, prefix=name_starts_with, results_per_page=results_per_page,
            page_iterator_class=BlobPropertiesPaged)


# test_finds_method_returning_something_else
class Some3Client():  # @
    def list_thing(self):  # @
        return list()

    def list_thing2(self):  # @
        return LROPoller()


# test_finds_method_returning_something_else_async
class Some4Client():  # @
    async def list_thing(self, **kwargs):  # @
        return list()

    async def list_thing2(self, **kwargs):  # @
        return LROPoller()


# test_finds_return_ItemPaged_not_list
class Some5Client():  # @
    def some_thing(self):  # @
        return ItemPaged()


# test_finds_return_AsyncItemPaged_not_list
class Some6Client():
    async def some_thing(self):  # @
        return AsyncItemPaged()

