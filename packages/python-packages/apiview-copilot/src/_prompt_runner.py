import json
from typing import Optional


def run_prompt(
    folder: str,
    filename: str,
    inputs: dict,
    settings=None,
    max_retries: int = 5,
    logger: Optional[object] = None,
) -> str:
    """
    Run a prompt with retry logic.

    Args:
        folder: Folder containing the prompt file
        filename: Name of the prompt file
        inputs: Dictionary of inputs for the prompt
        settings: Optional settings object for API keys, etc.
        max_retries: Maximum number of retry attempts (default: 5)
        logger: Optional logger for warnings/errors

    Returns:
        String result of the prompt execution

    Raises:
        Exception: If all retry attempts fail
    """
    from ._credential import in_ci
    from ._retry import retry_with_backoff
    from ._settings import SettingsManager
    from ._utils import run_prompty

    def execute_prompt() -> str:
        if in_ci():
            configuration = {"api_key": (settings or SettingsManager()).get("OPENAI_API_KEY")}
        else:
            configuration = {}
        return run_prompty(folder=folder, filename=filename, inputs=inputs, configuration=configuration)

    def on_retry(exception, attempt, max_attempts):
        if logger:
            logger.warning(f"Error executing prompt {filename}, attempt {attempt+1}/{max_attempts}: {str(exception)}")

    def on_failure(exception, attempt):
        if logger:
            logger.error(f"Failed to execute prompt {filename} after {attempt} attempts: {str(exception)}")
        raise exception

    return retry_with_backoff(
        func=execute_prompt,
        max_retries=max_retries,
        retry_exceptions=(json.JSONDecodeError, Exception),
        on_retry=on_retry,
        on_failure=on_failure,
        logger=logger,
        description=f"prompt {filename}",
    )
