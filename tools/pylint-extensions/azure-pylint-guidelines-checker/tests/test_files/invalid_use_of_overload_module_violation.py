# Test file for InvalidUseOfOverload checker - module-level violations

from typing import overload, Union

# Module-level: sync overloads but async implementation
@overload
def module_mixed(a: str) -> str:
    ...

@overload
def module_mixed(a: int) -> int:
    ...

async def module_mixed(a: Union[str, int]):  #@
    return a
