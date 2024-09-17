# Test file for InvalidUseOfOverload checker - testing what mypy doesn't pick up

from typing import Awaitable, overload, Union

@overload
def double(a: str):
    ...

@overload
def double(a: int):
    ...

async def double(a: Union[str, int]) -> int:
    if isinstance(a, str):
        return len(a)*2
    return a * 2