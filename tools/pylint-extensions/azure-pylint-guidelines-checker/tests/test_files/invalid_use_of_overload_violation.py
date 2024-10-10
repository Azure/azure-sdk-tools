# Test file for InvalidUseOfOverload checker - testing what mypy doesn't pick up

from typing import overload, Union

class testingOverload:
    @overload
    def double(a: str):
        ...

    @overload
    def double(a: int):
        ...

    async def double(a: Union[str, int]):
        if isinstance(a, str):
            return len(a)*2
        return a * 2


    @overload
    async def doubleAgain(a: str) -> int:
        ...

    @overload
    def doubleAgain(a: int) -> int:
        ...

    async def doubleAgain(a: Union[str, int]) -> int:
        if isinstance(a, str):
            return len(a)*2
        return a * 2