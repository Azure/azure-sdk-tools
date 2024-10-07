from azure.core.polling import LROPoller
from typing import Any


class MyClient():
    # test_begin_delete_operation_incorrect_return
    def begin_delete_some_function(self, **kwargs) -> LROPoller[Any]:  #@
        return LROPoller[Any]

    # test_delete_operation_incorrect_return
    def delete_some_function(self, **kwargs) -> str:  # @
        return "hello"

    # test_delete_operation_correct_return
    def delete_some_function(self, **kwargs) -> None:  # @
        return None

    # test_begin_delete_operation_correct_return
    def begin_delete_some_function(self, **kwargs) -> LROPoller[None]:  # @
        return LROPoller[None]
