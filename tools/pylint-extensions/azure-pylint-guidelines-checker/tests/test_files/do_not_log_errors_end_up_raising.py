import logging


# test_error_level_not_logged
def function1():
    try:  # @
        add = 1 + 2
    except Exception as e:
        logging.ERROR(str(e))  # @
        raise


# test_warning_level_not_logged
def function2():
    try:  # @
        add = 1 + 2
    except Exception as e:
        logging.warning(str(e))  # @
        raise


# test_warning_level_logging_ok_when_no_raise
def function3():
    try:
        add = 1 + 2
    except Exception as e:
        logging.warning(str(e))


# test_unlogged_exception_block
def function4():
    try:
        add = 1 + 2
    except Exception as e:
        raise


# test_mult_exception_blocks_separate_raise
def function5():
    try:
        add = 1 + 2
    except Exception as e:
        raise
    except OtherException as x:
        logging.error(str(x))


# test_mult_exception_blocks_with_raise
def function6():
    try:  # @
        add = 1 + 2
    except Exception as e:
        raise
    except OtherException as x:
        logging.error(str(x))  # @
        raise


# test_implicit_else_exception_logged
def function7():
    try:  # @
        add = 1 + 2
    except Exception as e:
        if e.code == "Retryable":
            logging.warning(f"Retryable failure occurred: {e}, attempting to restart")
            return True
        elif Exception != BaseException:
            logging.error(f"System shutting down due to error: {e}.")
            return False
        logging.error(f"Unexpected error occurred: {e}")  # @
        raise SystemError("Uh oh!") from e


# test_branch_exceptions_logged
def function8():
    try:  # @
        add = 1 + 2
    except Exception as e:
        if e.code == "Retryable":
            logging.warning(f"Retryable failure occurred: {e}, attempting to restart")  # @
            raise SystemError("Uh oh!") from e
        elif Exception != BaseException:
            logging.error(f"System shutting down due to error: {e}.")  # @
            raise SystemError("Uh oh!") from e
        elif e.code == "Second elif branch":
            logging.error(f"Second: {e}.")  # @
            raise SystemError("Uh oh!") from e
        logging.error(f"Unexpected error occurred: {e}")


# test_explicit_else_branch_exception_logged
def function9():
    try:  # @
        add = 1 + 2
    except Exception as e:
        if e.code == "Retryable":
            logging.warning(f"Retryable failure occurred: {e}, attempting to restart")
            return True
        elif Exception != BaseException:
            logging.error(f"System shutting down due to error: {e}.")
            return False
        else:
            logging.error(f"Unexpected error occurred: {e}")  # @
            raise SystemError("Uh oh!") from e


# test_extra_nested_branches_exception_logged
def function10():
    try:  # @
        add = 1 + 2
    except Exception as e:
        if e.code == "Retryable":
            if e.code == "A":
                logging.warning(f"A: {e}")  # @
                raise SystemError("Uh oh!") from e
            elif e.code == "E":
                logging.warning(f"E: {e}")  # @
                raise SystemError("Uh oh!") from e
            else:
                logging.warning(f"F: {e}")  # @
                raise SystemError("Uh oh!") from e
        else:
            logging.error(f"Unexpected error occurred: {e}")  # @
            raise SystemError("Uh oh!") from e
