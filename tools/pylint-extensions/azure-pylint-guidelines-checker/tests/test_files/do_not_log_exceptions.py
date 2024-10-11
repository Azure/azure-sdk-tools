from plugin import logger


# test_logging_levels_logged_str_exception
def test_logging_levels_logged_str_exception():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        logger.error("Error" + str(ex))  # @
        logger.warning(str(ex))
        logger.info(str(ex))
        logger.debug(str(ex))


# test_logging_levels_logged_repr_exception
def test_logging_levels_logged_repr_exception():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        logger.error(repr(ex))  # @
        logger.warning(repr(ex))
        logger.info(repr(ex))
        logger.debug(repr(ex))


# test_regular_logging_ok
def test_regular_logging_ok():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        logger.error("Example 1")  # @
        logger.warning("This is another example")
        logger.info("Random logging")
        logger.debug("Logging")


# test_logging_str_exception_branches
def test_logging_str_exception_branches():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        if ex.code == "Retryable":
            logger.error(str(ex))
            return True
        elif Exception != BaseException:
            logger.warning(repr(ex))
            return False
        else:
            logger.info(str(ex))  # @


# test_other_logging_fails
def test_other_logging_fails():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        if ex.code == "Retryable":
            logger.error("Something went wrong: {ex}. Try again")
            return True
        else:
            logger.warning(ex)
            return False


# test_no_logging_and_no_exception_name_ok
def test_no_logging_and_no_exception_name_ok():
    try:
        add = 1 + 2
    except Exception as ex:
        self.errors.appendleft(ex)
    except Exception as ex:  # pylint: disable=broad-except
        _logger.warning(
            "Exception occurred when instrumenting: %s.",
            lib_name,
            exc_info=ex,
        )
    except (OSError, PermissionError) as e:
        logger.warning(
            "Failed to read on-disk cache for component due to %s. "
            "Please check if the file %s is in use or current user doesn't have the permission.",
            type(e).__name__,
            on_disk_cache_path.as_posix(),
        )


# test_logging_without_exception_name
def test_logging_without_exception_name():
    try:
        add = 1 + 2
    except Exception as exception:
        if exception.code == "Retryable":
            _LOGGER.info(
                "%r returns an exception %r", self._container_id, last_exception
            )
        else:
            module_logger.debug("Skipping file upload, reason:  %s", str(e.reason))
