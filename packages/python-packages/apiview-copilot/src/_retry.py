# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Module containing retry logic with smart defaults, support for 'Retry-After' headers, and per-call timeout.
"""

import json
import time
from concurrent.futures import ThreadPoolExecutor, TimeoutError


class TimeoutException(Exception):
    """Custom exception for timeouts."""

    # pylint: disable=unnecessary-pass
    pass


def retry_with_backoff(
    func,
    *,
    max_retries=5,
    timeout=240,  # Timeout for each call in seconds (default: 4 minutes)
    retry_exceptions=(json.JSONDecodeError, TimeoutError, ConnectionError, TimeoutException),
    non_retryable_exceptions=(AttributeError, TypeError, NameError, SyntaxError, PermissionError),
    on_failure=None,
    on_retry=None,
    logger=None,
    description="operation",
):
    """
    Generic retry function with smart defaults, support for honoring 'Retry-After' headers, and per-call timeout.

    Args:
        func: The function to retry
        max_retries: Maximum number of retries
        timeout: Timeout for each function call in seconds
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
    e = None  # Ensure 'e' is always defined
    for attempt in range(max_retries):
        try:
            # Use ThreadPoolExecutor to enforce a timeout
            with ThreadPoolExecutor(max_workers=1) as executor:
                future = executor.submit(func)
                result = future.result(timeout=timeout)  # Wait for the result with a timeout
            return result
        except TimeoutError:
            if logger:
                logger.error(f"Timeout in {description}: Function execution exceeded {timeout} seconds")
            e = TimeoutException(f"Function execution exceeded {timeout} seconds")
        except Exception as exc:
            e = exc
            # Check if this is a non-retryable exception
            if isinstance(e, non_retryable_exceptions):
                if logger:
                    logger.error(f"Non-retryable error in {description}: {str(e)}")
                if on_failure:
                    return on_failure(e, attempt)
                raise  # Re-raise non-retryable exceptions immediately

            # Check if this is a retryable exception
            if not isinstance(e, retry_exceptions):
                if logger:
                    logger.error(f"Unhandled error in {description}: {str(e)}")
                if on_failure:
                    return on_failure(e, attempt)
                raise  # Re-raise exceptions that don't match retry_exceptions

            # This is a retryable exception
            error_msg = f"Error in {description}, attempt {attempt+1}/{max_retries}: {str(e)}"
            if logger:
                logger.error(error_msg)

        # Check for 'Retry-After' header if the exception has it
        retry_after = None
        if e is not None and hasattr(e, "response") and e.response is not None:  # pylint: disable=no-member
            retry_after = e.response.headers.get("Retry-After")  # pylint: disable=no-member
            if retry_after:
                try:
                    retry_after = int(retry_after)
                    if logger:
                        logger.info(f"Retry-After header found: {retry_after} seconds")
                except ValueError:
                    retry_after = None  # Ignore invalid Retry-After values

        # Use Retry-After if available, otherwise use exponential backoff
        if retry_after is None:
            retry_after = 2**attempt  # Exponential backoff
            if logger:
                logger.info(f"Using exponential backoff: {retry_after} seconds")

        # Call the on_retry callback if provided
        if on_retry:
            on_retry(e, attempt, max_retries)

        # Wait before retrying
        time.sleep(retry_after)

        # If this was the last attempt, call on_failure and return its result
        if attempt == max_retries - 1:
            if on_failure:
                return on_failure(e, attempt)
            raise e  # Raise the last caught exception

    # This shouldn't be reached, but just in case
    raise RuntimeError(f"Failed after {max_retries} attempts")
