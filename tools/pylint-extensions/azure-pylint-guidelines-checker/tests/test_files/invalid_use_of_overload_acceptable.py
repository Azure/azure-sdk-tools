# Test file for InvalidUseOfOverload checker

from typing import overload, Union

class testingOverload:
    @overload
    async def double(a: str):
        ...

    @overload
    async def double(a: int):
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
