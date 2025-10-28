import logging
import asyncio

# This should be flagged
logger = logging.getLogger(__name__)
my_log = logging.getLogger("test")

try:
    add = 1 + 2
except Exception as e:
    logging.exception(f"wrong {e}")  # @ should be flagged
    logger.exception(f"wrong {e}")  # @ should be flagged
    my_log.exception(f"wrong {e}")  # @ should be flagged
    logging.debug(f"right {e}")  # @ should NOT be flagged

# These should NOT be flagged (false positives in current implementation)
async def test_asyncio_task():
    task = asyncio.create_task(asyncio.sleep(1))
    task.cancel()
    
    try:
        await task
    except asyncio.CancelledError:
        exc = task.exception()  # @ should NOT be flagged - this is asyncio Task.exception()
        
class MyClass:
    def exception(self):
        return "not a logger"

def test_non_logger_exception():
    obj = MyClass()
    result = obj.exception()  # @ should NOT be flagged - this is not a logger
    
    # Another common false positive
    import concurrent.futures
    
    with concurrent.futures.ThreadPoolExecutor() as executor:
        future = executor.submit(lambda: 1/0)
        exc = future.exception()  # @ should NOT be flagged - this is Future.exception()