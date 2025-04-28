def retry_with_backoff(
    func,
    max_retries=5,
    retry_exceptions=(Exception,),
    on_failure=None,
    on_retry=None,
    logger=None,
    description="operation",
):
    """
    Generic retry function with exponential backoff.

    Args:
        func: The function to retry
        max_retries: Maximum number of retries
        retry_exceptions: Tuple of exceptions that should trigger a retry
        on_failure: Function to call on final failure (params: exception, attempt)
        on_retry: Function to call on each retry (params: exception, attempt, max_retries)
        logger: Logger object to use
        description: Description of the operation for logging

    Returns:
        The result of the function call, or the result of on_failure if all retries fail
    """
    for attempt in range(max_retries):
        try:
            result = func()
            return result
        except retry_exceptions as e:
            # Log the error
            error_msg = f"Error in {description}, attempt {attempt+1}/{max_retries}: {str(e)}"
            if logger:
                logger.error(error_msg)

            # Call the on_retry callback if provided
            if on_retry:
                on_retry(e, attempt, max_retries)

            # If this was the last attempt, call on_failure and return its result
            if attempt == max_retries - 1:
                if on_failure:
                    return on_failure(e, attempt)
                raise  # Re-raise the exception if no on_failure handler

    # This shouldn't be reached, but just in case
    raise RuntimeError(f"Failed after {max_retries} attempts")
