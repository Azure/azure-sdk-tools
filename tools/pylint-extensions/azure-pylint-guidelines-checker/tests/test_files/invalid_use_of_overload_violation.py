# Test file for InvalidUseOfOverload checker

from typing import Awaitable, overload, Union


@overload
def double(a: str)  -> Awaitable[int]:
    ...

@overload
def double(a: int):
    ...

async def double(a: Union[str, int]) -> int:
    if isinstance(a, str):
        return len(a)*2
    return a * 2