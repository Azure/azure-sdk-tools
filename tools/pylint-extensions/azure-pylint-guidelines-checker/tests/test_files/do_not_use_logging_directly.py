import logging

# Good: Creating logger instances (should NOT be flagged)
logger = logging.getLogger("test")
my_logger = logging.getLogger(__name__)
_log = logging.getLogger("app")

# Good: Configuration (should NOT be flagged)
logging.basicConfig(level=logging.DEBUG)

# Good: Accessing constants (should NOT be flagged)
level = logging.DEBUG
threshold = logging.WARNING

# Bad: Direct logging calls (SHOULD be flagged)
logging.info("direct call")
logging.debug("direct call")
logging.warning("direct call")
logging.error("direct call")
logging.critical("direct call")
logging.log(logging.INFO, "direct call")

# Good: Using logger instance (should NOT be flagged)
logger.info("using named logger")
logger.debug("using named logger")
my_logger.warning("using named logger")
_log.error("using named logger")


class MyClass:
    """Test class-level logger usage."""
    _logger = logging.getLogger(__name__)
    
    def method(self):
        self._logger.info("ok")  # should NOT be flagged
        logging.info("bad")  # SHOULD be flagged
