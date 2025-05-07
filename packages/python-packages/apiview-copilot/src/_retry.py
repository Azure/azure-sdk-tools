import json


def retry_with_backoff(
    func,
    *,
    max_retries=5,
    retry_exceptions=(json.JSONDecodeError, TimeoutError, ConnectionError),
    non_retryable_exceptions=(AttributeError, TypeError, NameError, SyntaxError, PermissionError),
    on_failure=None,
    on_retry=None,
    logger=None,
    description="operation",
):
    """
    Generic retry function with smart defaults.

    Args:
        func: The function to retry
        max_retries: Maximum number of retries
        retry_exceptions: Tuple of exceptions that should trigger a retry.
                         Default includes JSON parsing errors, timeouts, and connection issues.
        non_retryable_exceptions: Tuple of exceptions that should never be retried.
                                 Default includes programming errors that won't be fixed by retrying.
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
        except Exception as e:
            # Check if this is a non-retryable exception
            if isinstance(e, non_retryable_exceptions):
                if logger:
                    print(f"Non-retryable error in {description}: {str(e)}")
                if on_failure:
                    return on_failure(e, attempt)
                raise  # Re-raise non-retryable exceptions immediately

            # Check if this is a retryable exception
            if not isinstance(e, retry_exceptions):
                if logger:
                    print(f"Unhandled error in {description}: {str(e)}")
                if on_failure:
                    return on_failure(e, attempt)
                raise  # Re-raise exceptions that don't match retry_exceptions

            # This is a retryable exception
            error_msg = f"Error in {description}, attempt {attempt+1}/{max_retries}: {str(e)}"
            if logger:
                print(error_msg)

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
