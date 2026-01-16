"""Test that the linter detects asyncio.iscoroutinefunction usage."""

import asyncio

async def my_async_function():
    """A sample async function."""
    pass

def check_if_coroutine():
    """This should trigger the linter warning."""
    if asyncio.iscoroutinefunction(my_async_function):
        print("It's a coroutine")
