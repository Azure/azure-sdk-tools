import logging


# test_logging_levels_logged_str_exception
def test_logging_levels_logged_str_exception():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        logging.error("Error" + str(ex))  # @
        logging.warning(str(ex))
        logging.info(str(ex))
        logging.debug(str(ex))


# test_logging_levels_logged_repr_exception
def test_logging_levels_logged_repr_exception():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        logging.error(repr(ex))  # @
        logging.warning(repr(ex))
        logging.info(repr(ex))
        logging.debug(repr(ex))


# test_regular_logging_ok
def test_regular_logging_ok():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        logging.error("Example 1")  # @
        logging.warning("This is another example")
        logging.info("Random logging")
        logging.debug("Logging")


# test_logging_str_exception_branches
def test_logging_str_exception_branches():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        if ex.code == "Retryable":
            logging.error(str(ex))
            return True
        elif Exception != BaseException:
            logging.warning(repr(ex))
            return False
        else:
            logging.info(str(ex))  # @


# test_other_logging_fails
def test_other_logging_fails():
    try:  # @
        add = 1 + 2
    except Exception as ex:
        if ex.code == "Retryable":
            logging.error("Something went wrong: {ex}. Try again")
            return True
        else:
            logging.warning(ex)
            return False
