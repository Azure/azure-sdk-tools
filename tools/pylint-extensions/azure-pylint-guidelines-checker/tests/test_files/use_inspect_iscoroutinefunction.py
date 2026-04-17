# Test file for use-inspect-iscoroutinefunction checker

from asyncio import iscoroutinefunction

import asyncio

def check_func_with_import():
    async def my_async_func():
        pass
    
    # This should trigger a violation
    if asyncio.iscoroutinefunction(my_async_func):
        return True

import asyncio as aio

def check_func_with_alias():
    async def my_async_func():
        pass
    
    # This should trigger a violation even with alias
    if aio.iscoroutinefunction(my_async_func):
        return True

import inspect

def check_func_with_inspect():
    async def my_async_func():
        pass
    
    # This is the correct way - should not trigger violation
    if inspect.iscoroutinefunction(my_async_func):
        return True

def acceptable_asyncio_usage():
    import asyncio
    asyncio.run(None)
    asyncio.sleep(1)
