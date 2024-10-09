# Test file for InvalidUseOfOverload checker

from typing import Awaitable, overload, Union

class testingOverload:
    @overload
    def double(a: str)  -> Awaitable[int]:
        ...

    @overload
    def double(a: int) -> Awaitable[int]:
        ...

    async def double(a: Union[str, int]) -> int:
        if isinstance(a, str):
            return len(a)*2
        return a * 2


    @overload
    def single(a: str):
        ...

    @overload
    def single(a: int):
        ...

    def single(a: Union[str, int]) -> int:
        if isinstance(a, str):
            return len(a)
        return a
