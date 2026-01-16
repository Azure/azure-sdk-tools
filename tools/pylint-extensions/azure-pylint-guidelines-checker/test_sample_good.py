"""Test that the linter doesn't flag inspect.iscoroutinefunction usage."""

import inspect

async def my_async_function():
    """A sample async function."""
    pass

def check_if_coroutine():
    """This should NOT trigger the linter warning."""
    if inspect.iscoroutinefunction(my_async_function):
        print("It's a coroutine")
